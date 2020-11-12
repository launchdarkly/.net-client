﻿using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientOfflineTest
    {
        private const string sdkKey = "SDK_KEY";

        [Fact]
        public void OfflineClientHasNullDataSource()
        {
            var config = Configuration.Builder(sdkKey).Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void LddModeClientHasNullEventProcessor()
        {
            var config = Configuration.Builder(sdkKey).Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void OfflineClientIsInitialized()
        {
            var config = Configuration.Builder(sdkKey).Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void OfflineReturnsDefaultValue()
        {
            var config = Configuration.Builder(sdkKey).Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("x", client.StringVariation("key", User.WithKey("user"), "x"));
            }
        }

        [Fact]
        public void OfflineClientGetsFlagFromDataStore()
        {
            var dataStore = new InMemoryDataStore();
            TestUtils.UpsertFlag(dataStore,
                new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());
            var config = Configuration.Builder(sdkKey)
                .Offline(true)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal(true, client.BoolVariation("key", User.WithKey("user"), false));
            }
        }

        [Fact]
        public void OfflineClientStartupMessage()
        {
            var logCapture = Logs.Capture();
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(logCapture)).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(logCapture.HasMessageWithText(LogLevel.Info,
                    "Starting Launchdarkly client in offline mode."), logCapture.ToString());
            }
        }

        [Fact]
        public void TestSecureModeHash()
        {
            var config = Configuration.Builder("secret").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597",
                    client.SecureModeHash(User.WithKey("Message")));
            }
        }
    }
}
