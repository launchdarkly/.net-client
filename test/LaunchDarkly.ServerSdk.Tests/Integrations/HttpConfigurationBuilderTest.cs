﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class HttpConfigurationBuilderTest
    {
        private static readonly BasicConfiguration basicConfig =
            new BasicConfiguration("sdk-key", false, null);

        private readonly BuilderTestUtil<HttpConfigurationBuilder, IHttpConfiguration> _tester =
            BuilderTestUtil.For(() => Components.HttpConfiguration(),
                b => b.CreateHttpConfiguration(basicConfig));

        [Fact]
        public void ConnectTimeout()
        {
            var prop = _tester.Property(c => c.ConnectTimeout, (b, v) => b.ConnectTimeout(v));
            prop.AssertDefault(HttpConfigurationBuilder.DefaultConnectTimeout);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
        }

        [Fact]
        public void MessageHandler()
        {
            var prop = _tester.Property(c => c.MessageHandler, (b, v) => b.MessageHandler(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(new HttpClientHandler());
        }

        [Fact]
        public void ReadTimeout()
        {
            var prop = _tester.Property(c => c.ReadTimeout, (b, v) => b.ReadTimeout(v));
            prop.AssertDefault(HttpConfigurationBuilder.DefaultReadTimeout);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
        }

        [Fact]
        public void SdkKeyHeader()
        {
            var config = Components.HttpConfiguration().CreateHttpConfiguration(basicConfig);
            Assert.Equal(basicConfig.SdkKey, HeadersAsMap(config.DefaultHeaders)["authorization"]);
        }

        [Fact]
        public void UserAgentHeader()
        {
            var config = Components.HttpConfiguration().CreateHttpConfiguration(basicConfig);
            Assert.Equal("DotNetClient/" + AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)),
                HeadersAsMap(config.DefaultHeaders)["user-agent"]); // not configurable
        }

        [Fact]
        public void WrapperDefaultNone()
        {
            var config = Components.HttpConfiguration().CreateHttpConfiguration(basicConfig);
            Assert.False(HeadersAsMap(config.DefaultHeaders).ContainsKey("x-launchdarkly-wrapper"));
        }

        [Fact]
        public void WrapperNameOnly()
        {
            var config = Components.HttpConfiguration().Wrapper("w", null)
                .CreateHttpConfiguration(basicConfig);
            Assert.Equal("w", HeadersAsMap(config.DefaultHeaders)["x-launchdarkly-wrapper"]);
        }

        [Fact]
        public void WrapperNameAndVersion()
        {
            var config = Components.HttpConfiguration().Wrapper("w", "1.0")
                .CreateHttpConfiguration(basicConfig);
            Assert.Equal("w/1.0", HeadersAsMap(config.DefaultHeaders)["x-launchdarkly-wrapper"]);
        }

        private Dictionary<string, string> HeadersAsMap(IEnumerable<KeyValuePair<string, string>> headers)
        {
            return headers.ToDictionary(kv => kv.Key.ToLower(), kv => kv.Value);
        }
    }
}