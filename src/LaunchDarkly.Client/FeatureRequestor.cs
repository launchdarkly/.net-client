﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<FeatureRequestor>();
        private readonly Uri _uri;
        private volatile HttpClient _httpClient;
        private readonly Configuration _config;
        private volatile EntityTagHeaderValue _etag;

        internal FeatureRequestor(Configuration config)
        {
            _config = config;
            _uri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-flags");
            _httpClient = config.HttpClient();
        }

        // Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
        // was a problem getting flags.
        internal async Task<IDictionary<string, FeatureFlag>> MakeAllRequestAsync()
        {
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            try
            {
                return await FetchFeatureFlagsAsync(cts);
            }
            catch (Exception e)
            {
                // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                _httpClient?.Dispose();
                _httpClient = _config.HttpClient();

                Logger.LogDebug("Error getting feature flags: " + Util.ExceptionMessage(e) +
                                " waiting 1 second before retrying.");
                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                cts = new CancellationTokenSource(_config.HttpClientTimeout);
                try
                {
                    return await FetchFeatureFlagsAsync(cts);
                }
                catch (TaskCanceledException tce)
                {
                    // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                    _httpClient?.Dispose();
                    _httpClient = _config.HttpClient();

                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        throw tce;
                    }
                    //Otherwise this was a request timeout.
                    throw new Exception("Get Features with URL: " + _uri.AbsoluteUri + " timed out after : " +
                                        _config.HttpClientTimeout);
                }
                catch (Exception ex)
                {
                    // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                    _httpClient?.Dispose();
                    _httpClient = _config.HttpClient();
                    throw ex;
                }
            }
        }

        private async Task<IDictionary<string, FeatureFlag>> FetchFeatureFlagsAsync(CancellationTokenSource cts)
        {
            Logger.LogDebug("Getting all flags with uri: " + _uri.AbsoluteUri);
            var request = new HttpRequestMessage(HttpMethod.Get, _uri);
            if (_etag != null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            using (var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Logger.LogDebug("Get all flags returned 304: not modified");
                    return null;
                }
                _etag = response.Headers.ETag;
                //We ensure the status code after checking for 304, because 304 isn't considered success
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var flags = JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);
                Logger.LogDebug("Get all flags returned " + flags.Keys.Count + " feature flags");
                return flags;
            }
        }
    }
}