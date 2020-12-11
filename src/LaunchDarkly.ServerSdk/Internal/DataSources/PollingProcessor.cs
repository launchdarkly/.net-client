﻿using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class PollingProcessor : IDataSource
    {
        private readonly IFeatureRequestor _featureRequestor;
        private readonly IDataSourceUpdates _dataSourceUpdates;
        private readonly TaskExecutor _taskExecutor;
        private readonly TimeSpan _pollInterval;
        private readonly AtomicBoolean _initialized = new AtomicBoolean(false);
        private readonly TaskCompletionSource<bool> _initTask;
        private readonly Logger _log;
        private CancellationTokenSource _canceller;

        internal PollingProcessor(
            LdClientContext context,
            IFeatureRequestor featureRequestor,
            IDataSourceUpdates dataSourceUpdates,
            TimeSpan pollInterval
            )
        {
            _featureRequestor = featureRequestor;
            _dataSourceUpdates = dataSourceUpdates;
            _taskExecutor = context.TaskExecutor;
            _pollInterval = pollInterval;
            _initTask = new TaskCompletionSource<bool>();
            _log = context.Basic.Logger.SubLogger(LogNames.DataSourceSubLog);
        }

        bool IDataSource.Initialized()
        {
            return _initialized.Get();
        }

        Task<bool> IDataSource.Start()
        {
            lock (this)
            {
                if (_canceller == null) // means we already started
                {
                    _log.Info("Starting LaunchDarkly PollingProcessor with interval: {0} milliseconds",
                        _pollInterval.TotalMilliseconds);
                    _canceller = _taskExecutor.StartRepeatingTask(TimeSpan.Zero,
                        _pollInterval, () => UpdateTaskAsync());
                }
            }

            return _initTask.Task;
        }

        private async Task UpdateTaskAsync()
        {
            try
            {
                var allData = await _featureRequestor.GetAllDataAsync();
                if (allData is null)
                {
                    // This means it was cached, and alreadyInited was true
                    _dataSourceUpdates.UpdateStatus(DataSourceState.Valid, null);
                }
                else
                {
                    if (_dataSourceUpdates.Init(allData.ToInitData()))
                    {
                        _dataSourceUpdates.UpdateStatus(DataSourceState.Valid, null);

                        if (!_initialized.GetAndSet(true))
                        {
                            _initTask.SetResult(true);
                            _log.Info("Initialized LaunchDarkly Polling Processor.");
                        }
                    }
                }
            }
            catch (UnsuccessfulResponseException ex)
            {
                _log.Error(HttpErrors.ErrorMessage(ex.StatusCode, "polling request", "will retry"));
                var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(ex.StatusCode);
                if (HttpErrors.IsRecoverable(ex.StatusCode))
                {
                    _dataSourceUpdates.UpdateStatus(DataSourceState.Interrupted, errorInfo);
                }
                else
                {
                    _dataSourceUpdates.UpdateStatus(DataSourceState.Off, errorInfo);
                    try
                    {
                        // if client is initializing, make it stop waiting
                        _initTask.SetResult(true);
                    }
                    catch (InvalidOperationException)
                    {
                        // the task was already set - nothing more to do
                    }
                    ((IDisposable)this).Dispose();
                }
            }
            catch (JsonException ex)
            {
                _log.Error("Polling request received malformed data: {0}", LogValues.ExceptionSummary(ex));
                _dataSourceUpdates.UpdateStatus(DataSourceState.Interrupted,
                    new DataSourceStatus.ErrorInfo
                    {
                        Kind = DataSourceStatus.ErrorKind.InvalidData,
                        Time = DateTime.Now
                    });
            }
            catch (Exception ex)
            {
                Exception realEx = (ex is AggregateException ae) ? ae.Flatten() : ex;
                LogHelpers.LogException(_log, "Polling for feature flag updates failed", realEx);

                _dataSourceUpdates.UpdateStatus(DataSourceState.Interrupted,
                    DataSourceStatus.ErrorInfo.FromException(realEx));
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _log.Info("Stopping LaunchDarkly PollingProcessor");
                _canceller?.Cancel();
                _featureRequestor.Dispose();
            }
        }
    }
}