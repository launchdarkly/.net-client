﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace LaunchDarkly.Client.Files
{
    class FileDataSource : IUpdateProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileDataSource));
        private readonly IFeatureStore _featureStore;
        private readonly List<string> _paths;
        private readonly IDisposable _reloader;
        private volatile bool _started;
        private volatile bool _loadedValidData;
        
        public FileDataSource(IFeatureStore featureStore, List<string> paths, bool autoUpdate, TimeSpan pollInterval)
        {
            _featureStore = featureStore;
            _paths = new List<string>(paths);
            if (autoUpdate)
            {
                try
                {
#if NETSTANDARD1_4 || NETSTANDARD1_6
                    _reloader = new FilePollingReloader(_paths, TriggerReload, pollInterval);
#else
                    _reloader = new FileWatchingReloader(_paths, TriggerReload);
#endif
                }
                catch (Exception e)
                {
                    Log.Error("Unable to watch files for auto-updating: " + e);
                    _reloader = null;
                }
            }
            else
            {
                _reloader = null;
            }
        }

        public Task<bool> Start()
        {
            _started = true;
            LoadAll();

            // We always complete the start task regardless of whether we successfully loaded data or not;
            // if the data files were bad, they're unlikely to become good within the short interval that
            // LdClient waits on this task, even if auto-updating is on.
            TaskCompletionSource<bool> initTask = new TaskCompletionSource<bool>();
            initTask.SetResult(_loadedValidData);
            return initTask.Task;
        }

        public bool Initialized()
        {
            return _loadedValidData;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reloader?.Dispose();
            }
        }

        private void LoadAll()
        {
            Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
            foreach (var path in _paths)
            {
                try
                {
                    var content = File.ReadAllText(path);
                    var data = FlagFileData.FromFileContent(content);
                    data.AddToData(allData);
                }
                catch (Exception e)
                {
                    Log.Error(path + ": " + e.ToString());
                    return;
                }
            }
            _featureStore.Init(allData);
            _loadedValidData = true;
        }

        private void TriggerReload()
        {
            if (_started)
            {
                LoadAll();
            }
        }
    }
}
