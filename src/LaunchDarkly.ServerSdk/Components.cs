﻿using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    public static class Components
    {
        /// <summary>
        /// Returns a configuration object that disables direct connection with LaunchDarkly for feature
        /// flag updates.
        /// </summary>
        /// <remarks>
        /// Passing this to <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/> causes the SDK
        /// not to retrieve feature flag data from LaunchDarkly, regardless of any other configuration. This is
        /// normally done if you are using the <a href="https://docs.launchdarkly.com/home/advanced/relay-proxy">Relay Proxy</a>
        /// in "daemon mode", where an external process-- the Relay Proxy-- connects to LaunchDarkly and populates
        /// a persistent data store with the feature flag data. The data store could also be populated by
        /// another process that is running the LaunchDarkly SDK. If there is no external process updating
        /// the data store, then the SDK will not have any feature flag data and will return application
        /// default values only.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataSource(Components.ExternalUpdatesOnly)
        ///         .DataStore(Components.PersistentDataStore(Redis.DataStore())) // assuming the Relay Proxy is using Redis
        ///         .Build();
        /// </example>
        public static IDataSourceFactory ExternalUpdatesOnly => ComponentsImpl.NullDataSourceFactory.Instance;

        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IDataStore"/>.
        /// </summary>
        /// <remarks>
        /// Since it is the default, you do not normally need to call this method, unless you need to create
        /// a data store instance for testing purposes.
        /// </remarks>
        public static IDataStoreFactory InMemoryDataStore => ComponentsImpl.InMemoryDataStoreFactory.Instance;

        /// <summary>
        /// Returns a configuration builder for the SDK's logging configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Passing this to <see cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />,
        /// after setting any desired properties on the builder, applies this configuration to the SDK.
        /// </para>
        /// <para>
        /// The default behavior, if you do not change any properties, is to send log output to
        /// <see cref="Console.Error"/>, with a minimum level of <c>Info</c> (that is, <c>Debug</c> logging
        /// is disabled).
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
        /// </example>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="Components.Logging(ILogAdapter) "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging() =>
            new LoggingConfigurationBuilder();

        /// <summary>
        /// Returns a configuration builder for the SDK's logging configuration, specifying the logging implementation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a shortcut for calling <see cref="Logging()"/> and then
        /// <see cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)"/>, to specify a logging implementation
        /// other than the default one. For instance, in a .NET Core application you can use
        /// <c>LaunchDarkly.Logging.Logs.CoreLogging</c> to use the standard .NET Core logging framework.
        /// </para>
        /// <para>
        /// If you do not also specify a minimum logging level with <see cref="LoggingConfigurationBuilder.Level(LaunchDarkly.Logging.LogLevel)"/>,
        /// or with some other filtering mechanism that is defined by an external logging framework, then the
        /// log output will show all logging levels including <c>Debug</c>.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging(Logs.CoreLogging(coreLoggingFactory)))
        ///         .Build();
        /// </example>
        /// <param name="adapter">an <c>ILogAdapter</c> for the desired logging implementation</param>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="Components.Logging() "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging(ILogAdapter adapter) =>
            new LoggingConfigurationBuilder().Adapter(adapter);

        /// <summary>
        /// A configuration object that disables logging.
        /// </summary>
        /// <remarks>
        /// This is the same as <c>Logging(LaunchDarkly.Logging.Logs.None)</c>.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.NoLogging)
        ///         .Build();
        /// </example>
        public static LoggingConfigurationBuilder NoLogging =>
            new LoggingConfigurationBuilder().Adapter(Logs.None);

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreConfiguration"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreConfiguration"/>
        /// object to <see cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreConfiguration"/></returns>
        public static PersistentDataStoreConfiguration PersistentStore(IPersistentDataStoreFactory storeFactory)
        {
            return new PersistentDataStoreConfiguration(storeFactory);
        }

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreConfiguration"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreConfiguration"/>
        /// object to <see cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreConfiguration"/></returns>
        public static PersistentDataStoreConfiguration PersistentStore(IPersistentDataStoreAsyncFactory storeFactory)
        {
            return new PersistentDataStoreConfiguration(storeFactory);
        }

        /// <summary>
        /// Returns a configurable factory for using polling mode to get feature flag data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is not the default behavior; by default, the SDK uses a streaming connection to receive feature flag
        /// data from LaunchDarkly. In polling mode, the SDK instead makes a new HTTP request to LaunchDarkly at regular
        /// intervals. HTTP caching allows it to avoid redundantly downloading data if there have been no changes, but
        /// polling is still less efficient than streaming and should only be used on the advice of LaunchDarkly support.
        /// </para>
        /// <para>
        /// To use polling mode, call this method to obtain a builder, change its properties with the
        /// <see cref="PollingDataSourceBuilder"/> methods, and pass it to
        /// <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
        /// </para>
        /// <para>
        /// Setting <see cref="IConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will superseded this
        /// setting and completely disable network requests.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataSource(Components.PollingDataSource()
        ///             .PollInterval(TimeSpan.FromSeconds(45)))
        ///         .Build();
        /// </example>
        /// <returns>a builder for setting polling connection properties</returns>
        /// <see cref="StreamingDataSource"/>
        /// <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>
        public static PollingDataSourceBuilder PollingDataSource() =>
            new PollingDataSourceBuilder();

        /// <summary>
        /// Returns a configurable factory for using streaming mode to get feature flag data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, the SDK uses a streaming connection to receive feature flag data from LaunchDarkly. To use
        /// the default behavior, you do not need to call this method. However, if you want to customize the behavior
        /// of the connection, call this method to obtain a builder, change its properties with the
        /// <see cref="StreamingDataSourceBuilder"/> methods, and pass it to
        /// <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
        /// </para>
        /// <para>
        /// Setting <see cref="IConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will superseded this
        /// setting and completely disable network requests.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataSource(Components.StreamingDataSource()
        ///             .InitialReconnectDelay(TimeSpan.FromMilliseconds(500)))
        ///         .Build();
        /// </example>
        /// <returns>a builder for setting streaming connection properties</returns>
        /// <see cref="PollingDataSource"/>
        /// <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>
        public static StreamingDataSourceBuilder StreamingDataSource() =>
            new StreamingDataSourceBuilder();

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IEventProcessor"/>, which
        /// forwards all analytics events to LaunchDarkly (unless the client is offline).
        /// </summary>
        public static IEventProcessorFactory DefaultEventProcessor =>
            ComponentsImpl.DefaultEventProcessorFactory.Instance;

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IEventProcessor"/>, which will
        /// discard all analytics events and not send them to LaunchDarkly, regardless of any
        /// other configuration.
        /// </summary>
        public static IEventProcessorFactory NullEventProcessor =>
            ComponentsImpl.NullEventProcessorFactory.Instance;
    }

}
