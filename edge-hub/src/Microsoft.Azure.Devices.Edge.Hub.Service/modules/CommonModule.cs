// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;
    using Microsoft.Extensions.Logging;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Service.Constants;
    using MetricsListener = Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net.MetricsListener;

    public class CommonModule : Module
    {
        readonly string productInfo;
        readonly string iothubHostName;
        readonly string edgeDeviceId;
        readonly string edgeHubModuleId;
        readonly string edgeDeviceHostName;
        readonly Option<string> edgeHubGenerationId;
        readonly AuthenticationMode authenticationMode;
        readonly Option<string> edgeHubConnectionString;
        readonly bool optimizeForPerformance;
        readonly bool usePersistentStorage;
        readonly string storagePath;
        readonly TimeSpan scopeCacheRefreshRate;
        readonly Option<string> workloadUri;
        readonly Option<string> workloadApiVersion;
        readonly bool persistTokens;
        readonly IList<X509Certificate2> trustBundle;
        readonly string proxy;
        readonly MetricsConfig metricsConfig;
        readonly bool useBackupAndRestore;
        readonly string storageBackupPath;

        public CommonModule(
            string productInfo,
            string iothubHostName,
            string edgeDeviceId,
            string edgeHubModuleId,
            string edgeDeviceHostName,
            Option<string> edgeHubGenerationId,
            AuthenticationMode authenticationMode,
            Option<string> edgeHubConnectionString,
            bool optimizeForPerformance,
            bool usePersistentStorage,
            string storagePath,
            Option<string> workloadUri,
            Option<string> workloadApiVersion,
            TimeSpan scopeCacheRefreshRate,
            bool persistTokens,
            IList<X509Certificate2> trustBundle,
            string proxy,
            MetricsConfig metricsConfig,
            bool useBackupAndRestore,
            string storageBackupPath)
        {
            this.productInfo = productInfo;
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.edgeHubModuleId = Preconditions.CheckNonWhiteSpace(edgeHubModuleId, nameof(edgeHubModuleId));
            this.edgeDeviceHostName = Preconditions.CheckNotNull(edgeDeviceHostName, nameof(edgeDeviceHostName));
            this.edgeHubGenerationId = edgeHubGenerationId;
            this.authenticationMode = authenticationMode;
            this.edgeHubConnectionString = edgeHubConnectionString;
            this.optimizeForPerformance = optimizeForPerformance;
            this.usePersistentStorage = usePersistentStorage;
            this.storagePath = storagePath;
            this.scopeCacheRefreshRate = scopeCacheRefreshRate;
            this.workloadUri = workloadUri;
            this.workloadApiVersion = workloadApiVersion;
            this.persistTokens = persistTokens;
            this.trustBundle = Preconditions.CheckNotNull(trustBundle, nameof(trustBundle));
            this.proxy = Preconditions.CheckNotNull(proxy, nameof(proxy));
            this.metricsConfig = Preconditions.CheckNotNull(metricsConfig, nameof(metricsConfig));
            this.useBackupAndRestore = useBackupAndRestore;
            this.storageBackupPath = storageBackupPath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IMetricsListener
            builder.Register(
                    c =>
                        this.metricsConfig.Enabled
                            ? new MetricsListener(this.metricsConfig.ListenerConfig, c.Resolve<IMetricsProvider>())
                            : new NullMetricsListener() as IMetricsListener)
                .As<IMetricsListener>()
                .SingleInstance();

            // IMetricsProvider
            builder.Register(
                    c =>
                        this.metricsConfig.Enabled
                            ? new MetricsProvider(MetricsConstants.EdgeHubMetricPrefix, this.iothubHostName, this.edgeDeviceId)
                            : new NullMetricsProvider() as IMetricsProvider)
                .As<IMetricsProvider>()
                .SingleInstance();

            // ISignatureProvider
            builder.Register(
                    c =>
                    {
                        ISignatureProvider signatureProvider = this.edgeHubConnectionString.Map(
                                cs =>
                                {
                                    IotHubConnectionStringBuilder csBuilder = IotHubConnectionStringBuilder.Create(cs);
                                    return new SharedAccessKeySignatureProvider(csBuilder.SharedAccessKey) as ISignatureProvider;
                                })
                            .GetOrElse(
                                () =>
                                {
                                    string edgeHubGenerationId = this.edgeHubGenerationId.Expect(() => new InvalidOperationException("Generation ID missing"));
                                    string workloadUri = this.workloadUri.Expect(() => new InvalidOperationException("workloadUri is missing"));
                                    string workloadApiVersion = this.workloadApiVersion.Expect(() => new InvalidOperationException("workloadUri version is missing"));
                                    return new HttpHsmSignatureProvider(this.edgeHubModuleId, edgeHubGenerationId, workloadUri, workloadApiVersion, Constants.WorkloadApiVersion) as ISignatureProvider;
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
            builder.Register(c => new RocksDbOptionsProvider(c.Resolve<ISystemEnvironment>(), this.optimizeForPerformance))
                .As<IRocksDbOptionsProvider>()
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
                                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                                    c.Resolve<IRocksDbOptionsProvider>(),
                                    this.storagePath,
                                    partitionsList);
                                logger.LogInformation($"Created persistent store at {this.storagePath}");
                                return dbStoreprovider;
                            }
                            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
                            {
                                logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                                return new InMemoryDbStoreProvider(
                                    Option.Some(this.storageBackupPath),
                                    this.useBackupAndRestore);
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Using in-memory store");
                            return new InMemoryDbStoreProvider(
                                Option.Some(this.storageBackupPath),
                                this.useBackupAndRestore);
                        }
                    })
                .As<IDbStoreProvider>()
                .SingleInstance();

            // IProductInfoStore
            builder.Register(
                    c =>
                    {
                        var storeProvider = c.Resolve<IStoreProvider>();
                        IKeyValueStore<string, string> entityStore = storeProvider.GetEntityStore<string, string>("ProductInfo");
                        return new ProductInfoStore(entityStore, this.productInfo);
                    })
                .As<IProductInfoStore>()
                .SingleInstance();

            // Task<Option<IEncryptionProvider>>
            builder.Register(
                    async c =>
                    {
                        Option<IEncryptionProvider> encryptionProviderOption = await this.workloadUri
                            .Map(
                                async uri =>
                                {
                                    var encryptionProvider = await EncryptionProvider.CreateAsync(
                                        this.storagePath,
                                        new Uri(uri),
                                        this.workloadApiVersion.Expect(() => new InvalidOperationException("Missing workload API version")),
                                        Constants.WorkloadApiVersion,
                                        this.edgeHubModuleId,
                                        this.edgeHubGenerationId.Expect(() => new InvalidOperationException("Missing generation ID")),
                                        Constants.InitializationVectorFileName) as IEncryptionProvider;
                                    return Option.Some(encryptionProvider);
                                })
                            .GetOrElse(() => Task.FromResult(Option.None<IEncryptionProvider>()));
                        return encryptionProviderOption;
                    })
                .As<Task<Option<IEncryptionProvider>>>()
                .SingleInstance();

            // IStoreProvider
            builder.Register(c => new StoreProvider(c.Resolve<IDbStoreProvider>()))
                .As<IStoreProvider>()
                .SingleInstance();

            // ITokenProvider
            builder.Register(c => new ClientTokenProvider(c.Resolve<ISignatureProvider>(), this.iothubHostName, this.edgeDeviceId, this.edgeHubModuleId, TimeSpan.FromHours(1)))
                .Named<ITokenProvider>("EdgeHubClientAuthTokenProvider")
                .SingleInstance();

            // ITokenProvider
            builder.Register(
                    c =>
                    {
                        string deviceId = WebUtility.UrlEncode(this.edgeDeviceId);
                        string moduleId = WebUtility.UrlEncode(this.edgeHubModuleId);
                        return new ClientTokenProvider(c.Resolve<ISignatureProvider>(), this.iothubHostName, deviceId, moduleId, TimeSpan.FromHours(1));
                    })
                .Named<ITokenProvider>("EdgeHubServiceAuthTokenProvider")
                .SingleInstance();

            builder.Register(
                    c =>
                    {
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger<RoutingModule>();
                        return Proxy.Parse(this.proxy, logger);
                    })
                .As<Option<IWebProxy>>()
                .SingleInstance();

            // Task<IDeviceScopeIdentitiesCache>
            builder.Register(
                    async c =>
                    {
                        IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
                        if (this.authenticationMode == AuthenticationMode.CloudAndScope || this.authenticationMode == AuthenticationMode.Scope)
                        {
                            var edgeHubTokenProvider = c.ResolveNamed<ITokenProvider>("EdgeHubServiceAuthTokenProvider");
                            var proxy = c.Resolve<Option<IWebProxy>>();
                            IDeviceScopeApiClient securityScopesApiClient = new DeviceScopeApiClient(this.iothubHostName, this.edgeDeviceId, this.edgeHubModuleId, 10, edgeHubTokenProvider, proxy);
                            IServiceProxy serviceProxy = new ServiceProxy(securityScopesApiClient);
                            IKeyValueStore<string, string> encryptedStore = await GetEncryptedStore(c, "DeviceScopeCache");
                            deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy, encryptedStore, this.scopeCacheRefreshRate);
                        }
                        else
                        {
                            deviceScopeIdentitiesCache = new NullDeviceScopeIdentitiesCache();
                        }

                        return deviceScopeIdentitiesCache;
                    })
                .As<Task<IDeviceScopeIdentitiesCache>>()
                .AutoActivate()
                .SingleInstance();

            // Task<ICredentialsCache>
            builder.Register(
                    async c =>
                    {
                        ICredentialsCache underlyingCredentialsCache;
                        if (this.persistTokens)
                        {
                            IKeyValueStore<string, string> encryptedStore = await GetEncryptedStore(c, "CredentialsCache");
                            return new PersistedTokenCredentialsCache(encryptedStore);
                        }
                        else
                        {
                            underlyingCredentialsCache = new NullCredentialsCache();
                        }

                        ICredentialsCache credentialsCache = new CredentialsCache(underlyingCredentialsCache);
                        return credentialsCache;
                    })
                .As<Task<ICredentialsCache>>()
                .SingleInstance();

            // Task<IAuthenticator>
            builder.Register(
                    async c =>
                    {
                        IAuthenticator tokenAuthenticator;
                        IAuthenticator certificateAuthenticator;
                        IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
                        var credentialsCacheTask = c.Resolve<Task<ICredentialsCache>>();
                        // by default regardless of how the authenticationMode, X.509 certificate validation will always be scoped
                        deviceScopeIdentitiesCache = await c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                        certificateAuthenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, new NullAuthenticator(), this.trustBundle, true);
                        switch (this.authenticationMode)
                        {
                            case AuthenticationMode.Cloud:
                                tokenAuthenticator = await this.GetCloudTokenAuthenticator(c);
                                break;

                            case AuthenticationMode.Scope:
                                tokenAuthenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, this.iothubHostName, this.edgeDeviceHostName, new NullAuthenticator(), true, true);
                                break;

                            default:
                                IAuthenticator cloudTokenAuthenticator = await this.GetCloudTokenAuthenticator(c);
                                tokenAuthenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, this.iothubHostName, this.edgeDeviceHostName, cloudTokenAuthenticator, true, true);
                                break;
                        }

                        ICredentialsCache credentialsCache = await credentialsCacheTask;
                        return new Authenticator(tokenAuthenticator, certificateAuthenticator, credentialsCache) as IAuthenticator;
                    })
                .As<Task<IAuthenticator>>()
                .SingleInstance();

            // IClientCredentialsFactory
            builder.Register(c => new ClientCredentialsFactory(c.Resolve<IIdentityProvider>(), this.productInfo))
                .As<IClientCredentialsFactory>()
                .SingleInstance();

            // ConnectionReauthenticator
            builder.Register(
                    async c =>
                    {
                        var edgeHubCredentials = c.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
                        var connectionManagerTask = c.Resolve<Task<IConnectionManager>>();
                        var authenticatorTask = c.Resolve<Task<IAuthenticator>>();
                        var credentialsCacheTask = c.Resolve<Task<ICredentialsCache>>();
                        var deviceScopeIdentitiesCacheTask = c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                        var deviceConnectivityManager = c.Resolve<IDeviceConnectivityManager>();
                        IConnectionManager connectionManager = await connectionManagerTask;
                        IAuthenticator authenticator = await authenticatorTask;
                        ICredentialsCache credentialsCache = await credentialsCacheTask;
                        IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = await deviceScopeIdentitiesCacheTask;
                        var connectionReauthenticator = new ConnectionReauthenticator(
                            connectionManager,
                            authenticator,
                            credentialsCache,
                            deviceScopeIdentitiesCache,
                            TimeSpan.FromMinutes(5),
                            edgeHubCredentials.Identity,
                            deviceConnectivityManager);
                        return connectionReauthenticator;
                    })
                .As<Task<ConnectionReauthenticator>>()
                .SingleInstance();

            base.Load(builder);
        }

        static async Task<IKeyValueStore<string, string>> GetEncryptedStore(IComponentContext context, string entityName)
        {
            var storeProvider = context.Resolve<IStoreProvider>();
            Option<IEncryptionProvider> encryptionProvider = await context.Resolve<Task<Option<IEncryptionProvider>>>();
            IKeyValueStore<string, string> encryptedStore = encryptionProvider
                .Map(
                    e =>
                    {
                        IEntityStore<string, string> entityStore = storeProvider.GetEntityStore<string, string>(entityName);
                        IKeyValueStore<string, string> es = new EncryptedStore<string, string>(entityStore, e);
                        return es;
                    })
                .GetOrElse(() => new NullKeyValueStore<string, string>() as IKeyValueStore<string, string>);
            return encryptedStore;
        }

        async Task<IAuthenticator> GetCloudTokenAuthenticator(IComponentContext context)
        {
            IAuthenticator tokenAuthenticator;
            var connectionManagerTask = context.Resolve<Task<IConnectionManager>>();
            var credentialsCacheTask = context.Resolve<Task<ICredentialsCache>>();
            IConnectionManager connectionManager = await connectionManagerTask;
            ICredentialsCache credentialsCache = await credentialsCacheTask;
            if (this.persistTokens)
            {
                IAuthenticator authenticator = new CloudTokenAuthenticator(connectionManager, this.iothubHostName);
                tokenAuthenticator = new TokenCacheAuthenticator(authenticator, credentialsCache, this.iothubHostName);
            }
            else
            {
                tokenAuthenticator = new CloudTokenAuthenticator(connectionManager, this.iothubHostName);
            }

            return tokenAuthenticator;
        }
    }
}
