// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Client.Message;

    public class RoutingModule : Module
    {
        readonly string iotHubName;
        readonly string edgeDeviceId;
        readonly IDictionary<string, string> routes;
        readonly StoreAndForwardConfiguration storeAndForwardConfiguration;
        readonly int connectionPoolSize;
        readonly bool isStoreAndForwardEnabled;
        readonly bool usePersistentStorage;
        readonly string storagePath;
        readonly string edgeHubConnectionString;
        readonly bool useTwinConfig;
        readonly VersionInfo versionInfo;

        public RoutingModule(string iotHubName,
            string edgeDeviceId,
            string edgeHubConnectionString,
            IDictionary<string, string> routes,
            bool isStoreAndForwardEnabled,
            bool usePersistentStorage,
            StoreAndForwardConfiguration storeAndForwardConfiguration,
            string storagePath,
            int connectionPoolSize,
            bool useTwinConfig,
            VersionInfo versionInfo)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.edgeHubConnectionString = Preconditions.CheckNonWhiteSpace(edgeHubConnectionString, nameof(edgeHubConnectionString));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.storeAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
            this.usePersistentStorage = usePersistentStorage;
            this.storagePath = storagePath;
            this.connectionPoolSize = connectionPoolSize;
            this.useTwinConfig = useTwinConfig;
            this.versionInfo = versionInfo ?? VersionInfo.Empty;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IMessageConverter<IRoutingMessage>
            builder.Register(c => new RoutingMessageConverter())
                .As<Core.IMessageConverter<IRoutingMessage>>()
                .SingleInstance();

            // IRoutingPerfCounter
            builder.Register(
                c =>
                {
                    Routing.PerfCounter = NullRoutingPerfCounter.Instance;
                    return Routing.PerfCounter;
                })
                .As<IRoutingPerfCounter>()
                .AutoActivate()
                .SingleInstance();

            // IRoutingUserAnalyticsLogger
            builder.Register(
                c =>
                {
                    Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;
                    return Routing.UserAnalyticsLogger;
                })
                .As<IRoutingUserAnalyticsLogger>()
                .AutoActivate()
                .SingleInstance();

            // IRoutingUserMetricLogger
            builder.Register(
                c =>
                {
                    Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
                    return Routing.UserMetricLogger;
                })
                .As<IRoutingUserMetricLogger>()
                .AutoActivate()
                .SingleInstance();

            // IMessageConverter<Message>
            builder.Register(c => new MqttMessageConverter())
                .As<Core.IMessageConverter<Message>>()
                .SingleInstance();

            // IMessageConverter<Twin>
            builder.Register(c => new TwinMessageConverter())
                .As<Core.IMessageConverter<Twin>>()
                .SingleInstance();

            // IMessageConverter<TwinCollection>
            builder.Register(c => new TwinCollectionMessageConverter())
                .As<Core.IMessageConverter<TwinCollection>>()
                .SingleInstance();

            // IMessageConverterProvider
            builder.Register(
                c => new MessageConverterProvider(new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), c.Resolve<Core.IMessageConverter<Message>>() },
                    { typeof(Twin), c.Resolve<Core.IMessageConverter<Twin>>() },
                    { typeof(TwinCollection), c.Resolve<Core.IMessageConverter<TwinCollection>>() }
                }))
                .As<Core.IMessageConverterProvider>()
                .SingleInstance();

            // ICloudProxyProvider
            builder.Register(c => new CloudProxyProvider(c.Resolve<Core.IMessageConverterProvider>(), this.connectionPoolSize, !this.isStoreAndForwardEnabled))
                .As<ICloudProxyProvider>()
                .SingleInstance();

            // IConnectionManager
            builder.Register(c => new ConnectionManager(c.Resolve<ICloudProxyProvider>()))
                .As<IConnectionManager>()
                .SingleInstance();

            // IEndpointFactory
            builder.Register(c => new EndpointFactory(c.Resolve<IConnectionManager>(), c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), this.edgeDeviceId))
                .As<IEndpointFactory>()
                .SingleInstance();

            // RouteFactory
            builder.Register(c => new EdgeRouteFactory(c.Resolve<IEndpointFactory>()))
                .As<RouteFactory>()
                .SingleInstance();

            // RouterConfig
            builder.Register(c => new RouterConfig(Enumerable.Empty<Route>()))
                .As<RouterConfig>()
                .SingleInstance();

            if (!this.isStoreAndForwardEnabled)
            {
                // EndpointExecutorConfig
                builder.Register(
                    c =>
                    {
                        RetryStrategy defaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
                        TimeSpan defaultRevivePeriod = TimeSpan.FromHours(1);
                        TimeSpan defaultTimeout = TimeSpan.FromSeconds(60);
                        return new EndpointExecutorConfig(defaultTimeout, defaultRetryStrategy, defaultRevivePeriod, true);
                    })
                    .As<EndpointExecutorConfig>()
                    .SingleInstance();

                // IEndpointExecutorFactory
                builder.Register(c => new SyncEndpointExecutorFactory(c.Resolve<EndpointExecutorConfig>()))
                    .As<IEndpointExecutorFactory>()
                    .SingleInstance();

                // Task<Router>
                builder.Register(
                    async c =>
                    {
                        var endpointExecutorFactory = c.Resolve<IEndpointExecutorFactory>();
                        var routerConfig = c.Resolve<RouterConfig>();
                        Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, routerConfig, endpointExecutorFactory);
                        return router;
                    })
                    .As<Task<Router>>()
                    .SingleInstance();

                // ITwinManager
                builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.None<IStoreProvider>()))
                    .As<ITwinManager>()
                    .SingleInstance();
            }
            else
            {
                // EndpointExecutorConfig
                builder.Register(
                    c =>
                    {
                        // Endpoint executor config values -
                        // ExponentialBackoff - minBackoff = 10s, maxBackoff = 600s, delta (used to add randomness to backoff) - 10s (default)
                        // Num of retries = 75 for total retry period of about 12 hours.
                        // Revive period - period for which the endpoint should be considered dead if it doesn't respond - 1 min (we want to try continuously till the message expires)
                        // Timeout - time for which we want for the ack from the endpoint = 60s
                        // TODO - Num of retries should be set to (Store and forward timeout secs / minBackoff) + 1 (this gives us an upper limit on the number of times we need to retry)
                        // Need to make the number of retries dynamically configurable for that.

                        TimeSpan minWait = TimeSpan.FromSeconds(10);
                        TimeSpan maxWait = TimeSpan.FromSeconds(600);
                        TimeSpan delta = TimeSpan.FromSeconds(10);
                        int retries = 75;
                        RetryStrategy retryStrategy = new ExponentialBackoff(retries, minWait, maxWait, delta);
                        TimeSpan revivePeriod = TimeSpan.FromMinutes(1);
                        TimeSpan timeout = TimeSpan.FromSeconds(60);
                        return new EndpointExecutorConfig(timeout, retryStrategy, revivePeriod);
                    })
                    .As<EndpointExecutorConfig>()
                    .SingleInstance();


                // IDbStore
                builder.Register(
                    c =>
                    {
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        ILogger logger = loggerFactory.CreateLogger(typeof(RoutingModule));

                        if (this.usePersistentStorage)
                        {
                            // Create partitions for messages and twins
                            var partitionsList = new List<string> { Core.Constants.MessageStorePartitionKey, Core.Constants.TwinStorePartitionKey, Core.Constants.CheckpointStorePartitionKey };
                            try
                            {
                                IDbStoreProvider dbStoreprovider = Storage.RocksDb.DbStoreProvider.Create(this.storagePath, partitionsList);
                                logger.LogInformation($"Created persistent store at {this.storagePath}");
                                return dbStoreprovider;
                            }
                            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
                            {
                                logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                                return new InMemoryDbStoreProvider();
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Using in-memory store");
                            return new InMemoryDbStoreProvider();
                        }
                    })
                    .As<IDbStoreProvider>()
                    .SingleInstance();

                // ICheckpointStore
                builder.Register(c => CheckpointStore.Create(c.Resolve<IDbStoreProvider>()))
                    .As<ICheckpointStore>()
                    .SingleInstance();

                // IMessageStore
                builder.Register(
                   c =>
                   {
                       var checkpointStore = c.Resolve<ICheckpointStore>();
                       var dbStoreProvider = c.Resolve<IDbStoreProvider>();
                       IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
                       IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.MaxValue);
                       return messageStore;
                   })
                  .As<IMessageStore>()
                  .SingleInstance();

                // IEndpointExecutorFactory
                builder.Register(
                    c =>
                    {
                        var endpointExecutorConfig = c.Resolve<EndpointExecutorConfig>();
                        var messageStore = c.Resolve<IMessageStore>();
                        IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1), messageStore);
                        return endpointExecutorFactory;
                    })
                   .As<IEndpointExecutorFactory>()
                   .SingleInstance();

                // Task<Router>
                builder.Register(
                    async c =>
                    {
                        var checkpointStore = c.Resolve<ICheckpointStore>();
                        var routerConfig = c.Resolve<RouterConfig>();
                        var endpointExecutorFactory = c.Resolve<IEndpointExecutorFactory>();
                        return await Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, routerConfig, endpointExecutorFactory, checkpointStore);
                    })
                    .As<Task<Router>>()
                    .SingleInstance();

                // ITwinManager
                builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.Some<IStoreProvider>(new StoreProvider(c.Resolve<IDbStoreProvider>()))))
                    .As<ITwinManager>()
                    .SingleInstance();
            }

            // IConfigSource
            builder.Register(
                async c =>
                {
                    var routeFactory = c.Resolve<RouteFactory>();

                    if (this.useTwinConfig)
                    {
                        var identityFactory = c.Resolve<IIdentityFactory>();
                        Try<IIdentity> edgeHubIdentity = identityFactory.GetWithConnectionString(this.edgeHubConnectionString);
                        if (!edgeHubIdentity.Success)
                        {
                            throw edgeHubIdentity.Exception;
                        }
                        var connectionManager = c.Resolve<IConnectionManager>();
                        var twinCollectionMessageConverter = c.Resolve<Core.IMessageConverter<TwinCollection>>();
                        var twinMessageConverter = c.Resolve<Core.IMessageConverter<Twin>>();
                        var twinManager = c.Resolve<ITwinManager>();
                        IConfigSource edgeHubConnection = await EdgeHubConnection.Create(
                            edgeHubIdentity.Value, twinManager, connectionManager,
                            routeFactory, twinCollectionMessageConverter,
                            twinMessageConverter, this.versionInfo
                        );
                        return edgeHubConnection;
                    }
                    else
                    {
                        return new LocalConfigSource(routeFactory, this.routes, this.storeAndForwardConfiguration);
                    }
                })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // Task<IEdgeHub>
            builder.Register(
                async c =>
                {
                    Router router = await c.Resolve<Task<Router>>();
                    IEdgeHub hub = new RoutingEdgeHub(router, c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), c.Resolve<IConnectionManager>(), c.Resolve<ITwinManager>(), this.edgeDeviceId);
                    return hub;
                })
                .As<Task<IEdgeHub>>()
                .SingleInstance();

            builder.Register(
                async c =>
                {
                    IMessageStore messageStore = this.isStoreAndForwardEnabled ? c.Resolve<IMessageStore>() : null;
                    Router router = await c.Resolve<Task<Router>>();
                    var configUpdater = new ConfigUpdater(router, messageStore);
                    return configUpdater;
                })
                .As<Task<ConfigUpdater>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
