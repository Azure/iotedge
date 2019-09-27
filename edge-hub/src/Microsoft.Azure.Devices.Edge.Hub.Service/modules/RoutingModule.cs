// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Client.Message;

    public class RoutingModule : Module
    {
        readonly string iotHubName;
        readonly string edgeDeviceId;
        readonly string edgeModuleId;
        readonly Option<string> connectionString;
        readonly IDictionary<string, string> routes;
        readonly StoreAndForwardConfiguration storeAndForwardConfiguration;
        readonly int connectionPoolSize;
        readonly bool isStoreAndForwardEnabled;
        readonly bool useTwinConfig;
        readonly VersionInfo versionInfo;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly TimeSpan connectivityCheckFrequency;
        readonly int maxConnectedClients;
        readonly TimeSpan cloudConnectionIdleTimeout;
        readonly bool closeCloudConnectionOnIdleTimeout;
        readonly TimeSpan operationTimeout;
        readonly Option<TimeSpan> minTwinSyncPeriod;
        readonly Option<TimeSpan> reportedPropertiesSyncFrequency;
        readonly bool useV1TwinManager;
        readonly int maxUpstreamBatchSize;
        readonly int upstreamFanOutFactor;
        readonly bool encryptTwinStore;
        readonly TimeSpan configUpdateFrequency;
        readonly ExperimentalFeatures experimentalFeatures;

        public RoutingModule(
            string iotHubName,
            string edgeDeviceId,
            string edgeModuleId,
            Option<string> connectionString,
            IDictionary<string, string> routes,
            bool isStoreAndForwardEnabled,
            StoreAndForwardConfiguration storeAndForwardConfiguration,
            int connectionPoolSize,
            bool useTwinConfig,
            VersionInfo versionInfo,
            Option<UpstreamProtocol> upstreamProtocol,
            TimeSpan connectivityCheckFrequency,
            int maxConnectedClients,
            TimeSpan cloudConnectionIdleTimeout,
            bool closeCloudConnectionOnIdleTimeout,
            TimeSpan operationTimeout,
            Option<TimeSpan> minTwinSyncPeriod,
            Option<TimeSpan> reportedPropertiesSyncFrequency,
            bool useV1TwinManager,
            int maxUpstreamBatchSize,
            int upstreamFanOutFactor,
            bool encryptTwinStore,
            TimeSpan configUpdateFrequency,
            ExperimentalFeatures experimentalFeatures)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.connectionString = Preconditions.CheckNotNull(connectionString, nameof(connectionString));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.storeAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            this.edgeModuleId = edgeModuleId;
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
            this.connectionPoolSize = connectionPoolSize;
            this.useTwinConfig = useTwinConfig;
            this.versionInfo = versionInfo ?? VersionInfo.Empty;
            this.upstreamProtocol = upstreamProtocol;
            this.connectivityCheckFrequency = connectivityCheckFrequency;
            this.maxConnectedClients = Preconditions.CheckRange(maxConnectedClients, 1);
            this.cloudConnectionIdleTimeout = cloudConnectionIdleTimeout;
            this.closeCloudConnectionOnIdleTimeout = closeCloudConnectionOnIdleTimeout;
            this.operationTimeout = operationTimeout;
            this.minTwinSyncPeriod = minTwinSyncPeriod;
            this.reportedPropertiesSyncFrequency = reportedPropertiesSyncFrequency;
            this.useV1TwinManager = useV1TwinManager;
            this.maxUpstreamBatchSize = maxUpstreamBatchSize;
            this.upstreamFanOutFactor = upstreamFanOutFactor;
            this.encryptTwinStore = encryptTwinStore;
            this.configUpdateFrequency = configUpdateFrequency;
            this.experimentalFeatures = experimentalFeatures;
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
            builder.Register(c => new DeviceClientMessageConverter())
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
                    c => new MessageConverterProvider(
                        new Dictionary<Type, IMessageConverter>()
                        {
                            { typeof(Message), c.Resolve<Core.IMessageConverter<Message>>() },
                            { typeof(Twin), c.Resolve<Core.IMessageConverter<Twin>>() },
                            { typeof(TwinCollection), c.Resolve<Core.IMessageConverter<TwinCollection>>() }
                        }))
                .As<IMessageConverterProvider>()
                .SingleInstance();

            // IDeviceConnectivityManager
            builder.Register(
                    c =>
                    {
                        var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                        IDeviceConnectivityManager deviceConnectivityManager = this.experimentalFeatures.DisableConnectivityCheck
                            ? new NullDeviceConnectivityManager()
                            : new DeviceConnectivityManager(this.connectivityCheckFrequency, TimeSpan.FromMinutes(2), edgeHubCredentials.Identity) as IDeviceConnectivityManager;
                        return deviceConnectivityManager;
                    })
                .As<IDeviceConnectivityManager>()
                .SingleInstance();

            // IDeviceClientProvider
            builder.Register(
                    c =>
                    {
                        IClientProvider underlyingClientProvider = new ClientProvider();
                        IClientProvider connectivityAwareClientProvider = new ConnectivityAwareClientProvider(underlyingClientProvider, c.Resolve<IDeviceConnectivityManager>());
                        return connectivityAwareClientProvider;
                    })
                .As<IClientProvider>()
                .SingleInstance();

            // Task<ICloudConnectionProvider>
            builder.Register(
                    async c =>
                    {
                        var productInfoStore = c.Resolve<IProductInfoStore>();
                        var messageConverterProvider = c.Resolve<IMessageConverterProvider>();
                        var clientProvider = c.Resolve<IClientProvider>();
                        var tokenProvider = c.ResolveNamed<ITokenProvider>("EdgeHubClientAuthTokenProvider");
                        var credentialsCacheTask = c.Resolve<Task<ICredentialsCache>>();
                        var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                        var deviceScopeIdentitiesCacheTask = c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                        var proxy = c.Resolve<Option<IWebProxy>>();
                        IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await deviceScopeIdentitiesCacheTask;
                        ICredentialsCache credentialsCache = await credentialsCacheTask;
                        ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                            messageConverterProvider,
                            this.connectionPoolSize,
                            clientProvider,
                            this.upstreamProtocol,
                            tokenProvider,
                            deviceScopeIdentitiesCache,
                            credentialsCache,
                            edgeHubCredentials.Identity,
                            this.cloudConnectionIdleTimeout,
                            this.closeCloudConnectionOnIdleTimeout,
                            this.operationTimeout,
                            proxy,
                            productInfoStore);
                        return cloudConnectionProvider;
                    })
                .As<Task<ICloudConnectionProvider>>()
                .SingleInstance();

            // IIdentityProvider
            builder.Register(_ => new IdentityProvider(this.iotHubName))
                .As<IIdentityProvider>()
                .SingleInstance();

            // Task<IConnectionManager>
            builder.Register(
                    async c =>
                    {
                        var cloudConnectionProviderTask = c.Resolve<Task<ICloudConnectionProvider>>();
                        var credentialsCacheTask = c.Resolve<Task<ICredentialsCache>>();
                        var identityProvider = c.Resolve<IIdentityProvider>();
                        ICloudConnectionProvider cloudConnectionProvider = await cloudConnectionProviderTask;
                        ICredentialsCache credentialsCache = await credentialsCacheTask;
                        IConnectionManager connectionManager = new ConnectionManager(
                            cloudConnectionProvider,
                            credentialsCache,
                            identityProvider,
                            this.maxConnectedClients);
                        return connectionManager;
                    })
                .As<Task<IConnectionManager>>()
                .SingleInstance();

            // Task<IEndpointFactory>
            builder.Register(
                    async c =>
                    {
                        var messageConverter = c.Resolve<Core.IMessageConverter<IRoutingMessage>>();
                        IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                        return new EndpointFactory(connectionManager, messageConverter, this.edgeDeviceId, this.maxUpstreamBatchSize, this.upstreamFanOutFactor) as IEndpointFactory;
                    })
                .As<Task<IEndpointFactory>>()
                .SingleInstance();

            // Task<RouteFactory>
            builder.Register(async c => new EdgeRouteFactory(await c.Resolve<Task<IEndpointFactory>>()) as RouteFactory)
                .As<Task<RouteFactory>>()
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

                // Task<ITwinManager>
                builder.Register(
                        async c =>
                        {
                            if (!this.useV1TwinManager)
                            {
                                var messageConverterProvider = c.Resolve<IMessageConverterProvider>();
                                IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                                ITwinManager twinManager = new PassThroughTwinManager(connectionManager, messageConverterProvider);
                                return twinManager;
                            }
                            else
                            {
                                var messageConverterProvider = c.Resolve<IMessageConverterProvider>();
                                IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                                return TwinManager.CreateTwinManager(connectionManager, messageConverterProvider, Option.None<IStoreProvider>());
                            }
                        })
                    .As<Task<ITwinManager>>()
                    .SingleInstance();
            }
            else
            {
                // EndpointExecutorConfig
                builder.Register(
                        c =>
                        {
                            // Endpoint executor config values -
                            // ExponentialBackoff - minBackoff = 1s, maxBackoff = 60s, delta (used to add randomness to backoff) - 1s (default)
                            // Num of retries = int.MaxValue(we want to keep retrying till the message is sent)
                            // Revive period - period for which the endpoint should be considered dead if it doesn't respond - 1 min (we want to try continuously till the message expires)
                            // Timeout - time for which we want for the ack from the endpoint = 30s
                            // TODO - Should the number of retries be tied to the Store and Forward ttl? Not
                            // doing that right now as that value can be changed at runtime, but these settings
                            // cannot. Need to make the number of retries dynamically configurable for that.
                            TimeSpan minWait = TimeSpan.FromSeconds(1);
                            TimeSpan maxWait = TimeSpan.FromSeconds(60);
                            TimeSpan delta = TimeSpan.FromSeconds(1);
                            int retries = int.MaxValue;
                            RetryStrategy retryStrategy = new ExponentialBackoff(retries, minWait, maxWait, delta);
                            TimeSpan timeout = TimeSpan.FromSeconds(30);
                            TimeSpan revivePeriod = TimeSpan.FromSeconds(30);
                            return new EndpointExecutorConfig(timeout, retryStrategy, revivePeriod);
                        })
                    .As<EndpointExecutorConfig>()
                    .SingleInstance();

                // ICheckpointStore
                builder.Register(
                        c =>
                        {
                            var dbStoreProvider = c.Resolve<IDbStoreProvider>();
                            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
                            return CheckpointStore.Create(storeProvider);
                        })
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
                            IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(10, TimeSpan.FromSeconds(10)), messageStore);
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

                // Task<ITwinManager>
                builder.Register(
                        async c =>
                        {
                            if (this.useV1TwinManager)
                            {
                                var dbStoreProvider = c.Resolve<IDbStoreProvider>();
                                var messageConverterProvider = c.Resolve<IMessageConverterProvider>();
                                IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                                return TwinManager.CreateTwinManager(connectionManager, messageConverterProvider, Option.Some<IStoreProvider>(new StoreProvider(dbStoreProvider)));
                            }
                            else
                            {
                                var messageConverterProvider = c.Resolve<IMessageConverterProvider>();
                                var deviceConnectivityManager = c.Resolve<IDeviceConnectivityManager>();
                                var connectionManagerTask = c.Resolve<Task<IConnectionManager>>();
                                IEntityStore<string, TwinStoreEntity> entityStore = await this.GetTwinStore(c);
                                IConnectionManager connectionManager = await connectionManagerTask;
                                ITwinManager twinManager = StoringTwinManager.Create(
                                    connectionManager,
                                    messageConverterProvider,
                                    entityStore,
                                    deviceConnectivityManager,
                                    new ReportedPropertiesValidator(),
                                    this.minTwinSyncPeriod,
                                    this.reportedPropertiesSyncFrequency);
                                return twinManager;
                            }
                        })
                    .As<Task<ITwinManager>>()
                    .SingleInstance();
            }

            // IClientCredentials "EdgeHubCredentials"
            builder.Register(
                    c =>
                    {
                        var identityFactory = c.Resolve<IClientCredentialsFactory>();
                        IClientCredentials edgeHubCredentials = this.connectionString.Map(cs => identityFactory.GetWithConnectionString(cs)).GetOrElse(
                            () => identityFactory.GetWithIotEdged(this.edgeDeviceId, this.edgeModuleId));
                        return edgeHubCredentials;
                    })
                .Named<IClientCredentials>("EdgeHubCredentials")
                .SingleInstance();

            // Task<IInvokeMethodHandler>
            builder.Register(
                    async c =>
                    {
                        IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                        return new InvokeMethodHandler(connectionManager) as IInvokeMethodHandler;
                    })
                .As<Task<IInvokeMethodHandler>>()
                .SingleInstance();

            // Task<ISubscriptionProcessor>
            builder.Register(
                    async c =>
                    {
                        var connectionManagerTask = c.Resolve<Task<IConnectionManager>>();
                        if (this.experimentalFeatures.DisableCloudSubscriptions)
                        {
                            return new LocalSubscriptionProcessor(await connectionManagerTask) as ISubscriptionProcessor;
                        }
                        else
                        {
                            var invokeMethodHandlerTask = c.Resolve<Task<IInvokeMethodHandler>>();
                            var deviceConnectivityManager = c.Resolve<IDeviceConnectivityManager>();
                            IConnectionManager connectionManager = await connectionManagerTask;
                            IInvokeMethodHandler invokeMethodHandler = await invokeMethodHandlerTask;
                            return new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager) as ISubscriptionProcessor;
                        }
                    })
                .As<Task<ISubscriptionProcessor>>()
                .SingleInstance();

            // Task<IEdgeHub>
            builder.Register(
                    async c =>
                    {
                        var routingMessageConverter = c.Resolve<Core.IMessageConverter<IRoutingMessage>>();
                        var routerTask = c.Resolve<Task<Router>>();
                        var twinManagerTask = c.Resolve<Task<ITwinManager>>();
                        var invokeMethodHandlerTask = c.Resolve<Task<IInvokeMethodHandler>>();
                        var connectionManagerTask = c.Resolve<Task<IConnectionManager>>();
                        var subscriptionProcessorTask = c.Resolve<Task<ISubscriptionProcessor>>();
                        Router router = await routerTask;
                        ITwinManager twinManager = await twinManagerTask;
                        IConnectionManager connectionManager = await connectionManagerTask;
                        IInvokeMethodHandler invokeMethodHandler = await invokeMethodHandlerTask;
                        ISubscriptionProcessor subscriptionProcessor = await subscriptionProcessorTask;
                        IEdgeHub hub = new RoutingEdgeHub(
                            router,
                            routingMessageConverter,
                            connectionManager,
                            twinManager,
                            this.edgeDeviceId,
                            invokeMethodHandler,
                            subscriptionProcessor);
                        return hub;
                    })
                .As<Task<IEdgeHub>>()
                .SingleInstance();

            // Task<ConfigUpdater>
            builder.Register(
                    async c =>
                    {
                        IMessageStore messageStore = this.isStoreAndForwardEnabled ? c.Resolve<IMessageStore>() : null;
                        var storageSpaceChecker = c.Resolve<IStorageSpaceChecker>();
                        Router router = await c.Resolve<Task<Router>>();
                        var configUpdater = new ConfigUpdater(router, messageStore, this.configUpdateFrequency, storageSpaceChecker);
                        return configUpdater;
                    })
                .As<Task<ConfigUpdater>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        RouteFactory routeFactory = await c.Resolve<Task<RouteFactory>>();
                        if (this.useTwinConfig)
                        {
                            var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                            var twinCollectionMessageConverter = c.Resolve<Core.IMessageConverter<TwinCollection>>();
                            var twinMessageConverter = c.Resolve<Core.IMessageConverter<Twin>>();
                            var twinManagerTask = c.Resolve<Task<ITwinManager>>();
                            var edgeHubTask = c.Resolve<Task<IEdgeHub>>();
                            ITwinManager twinManager = await twinManagerTask;
                            IEdgeHub edgeHub = await edgeHubTask;
                            IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                            IConfigSource edgeHubConnection = await EdgeHubConnection.Create(
                                edgeHubCredentials.Identity,
                                edgeHub,
                                twinManager,
                                connectionManager,
                                routeFactory,
                                twinCollectionMessageConverter,
                                twinMessageConverter,
                                this.versionInfo,
                                deviceScopeIdentitiesCache);
                            return edgeHubConnection;
                        }
                        else
                        {
                            return new LocalConfigSource(routeFactory, this.routes, this.storeAndForwardConfiguration);
                        }
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // Task<IConnectionProvider>
            builder.Register(
                    async c =>
                    {
                        var connectionManagerTask = c.Resolve<Task<IConnectionManager>>();
                        var edgeHubTask = c.Resolve<Task<IEdgeHub>>();
                        IConnectionManager connectionManager = await connectionManagerTask;
                        IEdgeHub edgeHub = await edgeHubTask;
                        IConnectionProvider connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
                        return connectionProvider;
                    })
                .As<Task<IConnectionProvider>>()
                .SingleInstance();

            base.Load(builder);
        }

        async Task<IEntityStore<string, TwinStoreEntity>> GetTwinStore(IComponentContext context)
        {
            string entityName = "EdgeTwin";
            Option<IEntityStore<string, TwinStoreEntity>> twinStoreOption = Option.None<IEntityStore<string, TwinStoreEntity>>();
            var storeProvider = context.Resolve<IStoreProvider>();
            if (this.encryptTwinStore)
            {
                Option<IEncryptionProvider> encryptionProvider = await context.Resolve<Task<Option<IEncryptionProvider>>>();
                twinStoreOption = encryptionProvider.Map(
                    e =>
                    {
                        IEntityStore<string, string> underlyingEntityStore = storeProvider.GetEntityStore<string, string>($"underlying{entityName}");
                        IKeyValueStore<string, string> es = new UpdatableEncryptedStore<string, string>(underlyingEntityStore, e);
                        ITypeMapper<string, string> keyMapper = new JsonMapper<string>();
                        ITypeMapper<TwinStoreEntity, string> valueMapper = new JsonMapper<TwinStoreEntity>();
                        IKeyValueStore<string, TwinStoreEntity> dbStoreMapper = new KeyValueStoreMapper<string, string, TwinStoreEntity, string>(es, keyMapper, valueMapper);
                        IEntityStore<string, TwinStoreEntity> tes = new EntityStore<string, TwinStoreEntity>(dbStoreMapper, entityName);
                        return tes;
                    });
            }

            IEntityStore<string, TwinStoreEntity> twinStore = twinStoreOption.GetOrElse(
                () => storeProvider.GetEntityStore<string, TwinStoreEntity>(entityName));
            return twinStore;
        }
    }
}
