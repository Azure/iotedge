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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using IRoutingMessage = Routing.Core.IMessage;
    using Message = Client.Message;
    using IAuthenticationMethod = Microsoft.Azure.Devices.Client.IAuthenticationMethod;

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
        readonly bool usePersistentStorage;
        readonly string storagePath;
        readonly bool useTwinConfig;
        readonly VersionInfo versionInfo;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly bool optimizeForPerformance;
        readonly TimeSpan connectivityCheckFrequency;
        readonly int maxConnectedClients;
        readonly bool cacheTokens;
        readonly Option<string> workloadUri;
        readonly Option<string> edgeModuleGenerationId;

        public RoutingModule(string iotHubName,
            string edgeDeviceId,
            string edgeModuleId,
            Option<string> connectionString,
            IDictionary<string, string> routes,
            bool isStoreAndForwardEnabled,
            bool usePersistentStorage,
            StoreAndForwardConfiguration storeAndForwardConfiguration,
            string storagePath,
            int connectionPoolSize,
            bool useTwinConfig,
            VersionInfo versionInfo,
            Option<UpstreamProtocol> upstreamProtocol,
            bool optimizeForPerformance,
            TimeSpan connectivityCheckFrequency,
            int maxConnectedClients,
            bool cacheTokens,
            Option<string> workloadUri,
            Option<string> edgeModuleGenerationId)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.connectionString = Preconditions.CheckNotNull(connectionString, nameof(connectionString));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.storeAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            this.edgeModuleId = edgeModuleId;
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
            this.usePersistentStorage = usePersistentStorage;
            this.storagePath = storagePath;
            this.connectionPoolSize = connectionPoolSize;
            this.useTwinConfig = useTwinConfig;
            this.versionInfo = versionInfo ?? VersionInfo.Empty;
            this.upstreamProtocol = upstreamProtocol;
            this.optimizeForPerformance = optimizeForPerformance;
            this.connectivityCheckFrequency = connectivityCheckFrequency;
            this.maxConnectedClients = Preconditions.CheckRange(maxConnectedClients, 1);
            this.cacheTokens = cacheTokens;
            this.workloadUri = workloadUri;
            this.edgeModuleGenerationId = edgeModuleGenerationId;
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
                c => new MessageConverterProvider(new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), c.Resolve<Core.IMessageConverter<Message>>() },
                    { typeof(Twin), c.Resolve<Core.IMessageConverter<Twin>>() },
                    { typeof(TwinCollection), c.Resolve<Core.IMessageConverter<TwinCollection>>() }
                }))
                .As<Core.IMessageConverterProvider>()
                .SingleInstance();

            // IDeviceConnectivityManager
            builder.Register(
                c =>
                {
                    IDeviceConnectivityManager deviceConnectivityManager = new DeviceConnectivityManager(this.connectivityCheckFrequency, TimeSpan.FromMinutes(2));
                    return deviceConnectivityManager;
                })
                .As<IDeviceConnectivityManager>()
                .SingleInstance();

            // IDeviceClientProvider
            builder.Register(c =>
                {
                    IClientProvider underlyingClientProvider = new ClientProvider();
                    IClientProvider connectivityAwareClientProvider = new ConnectivityAwareClientProvider(underlyingClientProvider, c.Resolve<IDeviceConnectivityManager>());
                    return connectivityAwareClientProvider;
                })
                .As<IClientProvider>()
                .SingleInstance();

            // ISignatureProvider
            builder.Register(
                    c =>
                    {
                        ISignatureProvider signatureProvider = this.connectionString.Map(
                                cs =>
                                {
                                    IotHubConnectionStringBuilder csBuilder = IotHubConnectionStringBuilder.Create(cs);
                                    return new SharedAccessKeySignatureProvider(csBuilder.SharedAccessKey) as ISignatureProvider;
                                })
                            .GetOrElse(
                                () =>
                                {
                                    string edgeHubGenerationId = this.edgeModuleGenerationId.Expect(() => new Exception("Generation ID missing"));
                                    return new HttpHsmSignatureProvider(this.edgeModuleId, edgeHubGenerationId, string.Empty, string.Empty) as ISignatureProvider;
                                });
                        return signatureProvider;
                    })
                .As<ISignatureProvider>()
                .SingleInstance();


            // Detect system environment
            builder.Register(c => new SystemEnvironment())
                .As<ISystemEnvironment>()
                .SingleInstance();

            // DataBase options
            builder.Register(c => new Storage.RocksDb.RocksDbOptionsProvider(c.Resolve<ISystemEnvironment>(), this.optimizeForPerformance))
                .As<Storage.RocksDb.IRocksDbOptionsProvider>()
                .SingleInstance();

            // IDbStoreProvider
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
                            IDbStoreProvider dbStoreprovider = Storage.RocksDb.DbStoreProvider.Create(c.Resolve<Storage.RocksDb.IRocksDbOptionsProvider>(),
                                this.storagePath, partitionsList);
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

            // Task<IEncryptionProvider>
            builder.Register(
                    async c =>
                    {
                        IEncryptionProvider encryptionProvider = await this.workloadUri.Map(
                                async uri => await EncryptionProvider.CreateAsync(
                                    this.storagePath,
                                    new Uri(uri),
                                    Service.Constants.WorkloadApiVersion,
                                    this.edgeModuleId,
                                    this.edgeModuleGenerationId.Expect(() => new InvalidOperationException("Missing generation ID")),
                                    Service.Constants.InitializationVectorFileName) as IEncryptionProvider)
                            .GetOrElse(() => Task.FromResult<IEncryptionProvider>(NullEncryptionProvider.Instance));
                        return encryptionProvider;
                    })
                .As<Task<IEncryptionProvider>>()
                .SingleInstance();

            // IStoreProvider
            builder.Register(c => new StoreProvider(c.Resolve<IDbStoreProvider>()))
                .As<IStoreProvider>()
                .SingleInstance();

            // ITokenProvider
            builder.Register(c => new EdgeHubTokenProvider(c.Resolve<ISignatureProvider>(), this.iotHubName, this.edgeDeviceId, this.edgeModuleId, TimeSpan.FromHours(1)))
                .Named<ITokenProvider>("EdgeHubClientAuthTokenProvider")
                .SingleInstance();

            // ITokenProvider
            builder.Register(c =>
                {
                    string deviceId = WebUtility.UrlEncode(this.edgeDeviceId);
                    string moduleId = WebUtility.UrlEncode(this.edgeModuleId);
                    return new EdgeHubTokenProvider(c.Resolve<ISignatureProvider>(), this.iotHubName, deviceId, moduleId, TimeSpan.FromHours(1));
                })
                .Named<ITokenProvider>("EdgeHubServiceAuthTokenProvider")
                .SingleInstance();

            // Task<ISecurityScopeEntitiesCache>
            builder.Register(
                    async c =>
                    {
                        var edgeHubTokenProvider = c.ResolveNamed<ITokenProvider>("EdgeHubServiceAuthTokenProvider");
                        ISecurityScopesApiClient securityScopesApiClient = new SecurityScopesApiClient(this.iotHubName, this.edgeDeviceId, this.edgeModuleId, 10, edgeHubTokenProvider);
                        IServiceProxy serviceProxy = new ServiceProxy(securityScopesApiClient);

                        var storeProvider = c.Resolve<IStoreProvider>();
                        IEncryptionProvider encryptionProvider = await c.Resolve<Task<IEncryptionProvider>>();
                        IEntityStore<string, string> entityStore = storeProvider.GetEntityStore<string, string>("SecurityScopeCache");
                        IEncryptedStore<string, string> encryptedStore = new EncryptedStore<string, string>(entityStore, encryptionProvider);
                        ISecurityScopeEntitiesCache securityScopeEntitiesCache = new SecurityScopeEntitiesCache(serviceProxy, encryptedStore);
                        return securityScopeEntitiesCache;
                    })
                .As<Task<ISecurityScopeEntitiesCache>>()
                .SingleInstance();

            // ICloudConnectionProvider
            builder.Register(async c =>
                {                    
                    var edgeHubTokenProvider = c.ResolveNamed<ITokenProvider>("EdgeHubClientAuthTokenProvider");
                    ISecurityScopeEntitiesCache securityScopeEntitiesCache = await c.Resolve<Task<ISecurityScopeEntitiesCache>>();
                    return new CloudConnectionProvider(c.Resolve<Core.IMessageConverterProvider>(), this.connectionPoolSize, c.Resolve<IClientProvider>(),
                        this.upstreamProtocol, edgeHubTokenProvider, securityScopeEntitiesCache);
                })
                .As<ICloudConnectionProvider>()
                .SingleInstance();

            // Task<ICredentialsStore>
            builder.Register(async c =>
                {
                    if (this.cacheTokens)
                    {
                        var storeProvider = c.Resolve<IStoreProvider>();
                        IEncryptionProvider encryptionProvider = await c.Resolve<Task<IEncryptionProvider>>();
                        IEntityStore<string, string> tokenCredentialsEntityStore = storeProvider.GetEntityStore<string, string>("tokenCredentials");
                        return new TokenCredentialsStore(tokenCredentialsEntityStore, encryptionProvider);
                    }
                    else
                    {
                        return new NullCredentialsStore() as ICredentialsStore;
                    }
                })
                .As<Task<ICredentialsStore>>()
                .SingleInstance();

            // IConnectionManager
            builder.Register(c => new ConnectionManager(c.Resolve<ICloudConnectionProvider>(), this.maxConnectedClients))
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
                builder.Register(c => CheckpointStore.Create(c.Resolve<IDbStoreProvider>()))
                    .As<ICheckpointStore>()
                    .SingleInstance();

                // IMessageStore
                builder.Register(
                   c =>
                   {
                       var checkpointStore = c.Resolve<ICheckpointStore>();
                       var storeProvider = c.Resolve<IStoreProvider>();
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

                // ITwinManager
                builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.Some<IStoreProvider>(new StoreProvider(c.Resolve<IDbStoreProvider>()))))
                    .As<ITwinManager>()
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

            // Task<ICloudProxy> "EdgeHubCloudProxy"
            builder.Register(
                    async c =>
                    {
                        var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                        var connectionManager = c.Resolve<IConnectionManager>();
                        Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(edgeHubCredentials);
                        if (!cloudProxyTry.Success)
                        {
                            throw new EdgeHubConnectionException("Edge hub is unable to connect to IoT Hub", cloudProxyTry.Exception);
                        }

                        ICloudProxy cloudProxy = cloudProxyTry.Value;
                        return cloudProxy;
                    })
                .Named<Task<ICloudProxy>>("EdgeHubCloudProxy")
                .SingleInstance();

            // IInvokeMethodHandler
            builder.Register(c => new InvokeMethodHandler(c.Resolve<IConnectionManager>()))
                .As<IInvokeMethodHandler>()
                .SingleInstance();

            // Task<IEdgeHub>
            builder.Register(
                async c =>
                {
                    Router router = await c.Resolve<Task<Router>>();
                    IEdgeHub hub = new RoutingEdgeHub(router, c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), c.Resolve<IConnectionManager>(),
                        c.Resolve<ITwinManager>(), this.edgeDeviceId, c.Resolve<IInvokeMethodHandler>());
                    return hub;
                })
                .As<Task<IEdgeHub>>()
                .SingleInstance();

            // Task<ConfigUpdater>
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

            // Task<IConfigSource>
            builder.Register(
                async c =>
                {
                    var routeFactory = c.Resolve<RouteFactory>();

                    if (this.useTwinConfig)
                    {
                        var connectionManager = c.Resolve<IConnectionManager>();
                        var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                        var twinCollectionMessageConverter = c.Resolve<Core.IMessageConverter<TwinCollection>>();
                        var twinMessageConverter = c.Resolve<Core.IMessageConverter<Twin>>();
                        var twinManager = c.Resolve<ITwinManager>();
                        ICloudProxy cloudProxy = await c.ResolveNamed<Task<ICloudProxy>>("EdgeHubCloudProxy");
                        IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                        IConfigSource edgeHubConnection = await EdgeHubConnection.Create(
                            edgeHubCredentials.Identity as IModuleIdentity,
                            edgeHub,
                            twinManager,
                            connectionManager,
                            cloudProxy,
                            routeFactory,
                            twinCollectionMessageConverter,
                            twinMessageConverter,
                            this.versionInfo
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

            // Task<IConnectionProvider>
            builder.Register(
                async c =>
                {
                    IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                    IConnectionProvider connectionProvider = new ConnectionProvider(c.Resolve<IConnectionManager>(), edgeHub);
                    return connectionProvider;
                })
                .As<Task<IConnectionProvider>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
