// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Extensions.Logging;

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
        readonly bool cacheTokens;

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
            TimeSpan scopeCacheRefreshRate,
            bool cacheTokens)
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
            this.cacheTokens = cacheTokens;
        }

        protected override void Load(ContainerBuilder builder)
        {
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
                                    return new HttpHsmSignatureProvider(this.edgeHubModuleId, edgeHubGenerationId, workloadUri, Service.Constants.WorkloadApiVersion) as ISignatureProvider;
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
                                    this.edgeHubModuleId,
                                    this.edgeHubGenerationId.Expect(() => new InvalidOperationException("Missing generation ID")),
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
            builder.Register(c => new ModuleTokenProvider(c.Resolve<ISignatureProvider>(), this.iothubHostName, this.edgeDeviceId, this.edgeHubModuleId, TimeSpan.FromHours(1)))
                .Named<ITokenProvider>("EdgeHubClientAuthTokenProvider")
                .SingleInstance();

            // ITokenProvider
            builder.Register(c =>
                {
                    string deviceId = WebUtility.UrlEncode(this.edgeDeviceId);
                    string moduleId = WebUtility.UrlEncode(this.edgeHubModuleId);
                    return new ModuleTokenProvider(c.Resolve<ISignatureProvider>(), this.iothubHostName, deviceId, moduleId, TimeSpan.FromHours(1));
                })
                .Named<ITokenProvider>("EdgeHubServiceAuthTokenProvider")
                .SingleInstance();

            // Task<IKeyValueStore<string, string>> - EncryptedStore
            builder.Register(
                    async c =>
                    {
                        var storeProvider = c.Resolve<IStoreProvider>();
                        IEncryptionProvider encryptionProvider = await c.Resolve<Task<IEncryptionProvider>>();
                        IEntityStore<string, string> entityStore = storeProvider.GetEntityStore<string, string>("SecurityScopeCache");
                        IKeyValueStore<string, string> encryptedStore = new EncryptedStore<string, string>(entityStore, encryptionProvider);
                        return encryptedStore;
                    })
                .Named<Task<IKeyValueStore<string, string>>>("EncryptedStore")
                .SingleInstance();


            // Task<IDeviceScopeIdentitiesCache>
            builder.Register(
                    async c =>
                    {
                        IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
                        if (this.authenticationMode == AuthenticationMode.CloudAndScope || this.authenticationMode == AuthenticationMode.Scope)
                        {
                            var edgeHubTokenProvider = c.ResolveNamed<ITokenProvider>("EdgeHubServiceAuthTokenProvider");
                            IDeviceScopeApiClient securityScopesApiClient = new DeviceScopeApiClient(this.iothubHostName, this.edgeDeviceId, this.edgeHubModuleId, 10, edgeHubTokenProvider);
                            IServiceProxy serviceProxy = new ServiceProxy(securityScopesApiClient);
                            IKeyValueStore<string, string> encryptedStore = await c.ResolveNamed<Task<IKeyValueStore<string, string>>>("EncryptedStore");
                            deviceScopeIdentitiesCache = await DeviceScopeIdentitiesCache.Create(serviceProxy, encryptedStore, this.scopeCacheRefreshRate);
                        }
                        else
                        {
                            deviceScopeIdentitiesCache = new NullDeviceScopeIdentitiesCache();
                        }

                        return deviceScopeIdentitiesCache;
                    })
                .As<Task<IDeviceScopeIdentitiesCache>>()
                .SingleInstance();

            // Task<IAuthenticator>
            builder.Register(async c =>
                {
                    IConnectionManager connectionManager = await c.Resolve<Task<IConnectionManager>>();
                    IAuthenticator tokenAuthenticator;
                    IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
                    switch (this.authenticationMode)
                    {
                        case AuthenticationMode.Cloud:
                            if (this.cacheTokens)
                            {
                                ICredentialsCache credentialsCache = await c.Resolve<Task<ICredentialsCache>>();
                                IAuthenticator authenticator = new CloudTokenAuthenticator(connectionManager, this.iothubHostName);
                                tokenAuthenticator = new TokenCacheAuthenticator(authenticator, credentialsCache, this.iothubHostName);
                            }
                            else
                            {
                                tokenAuthenticator = new CloudTokenAuthenticator(connectionManager);
                            }
                            break;

                        case AuthenticationMode.Scope:
                            deviceScopeIdentitiesCache = await c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                            tokenAuthenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, this.iothubHostName, this.edgeDeviceHostName, new NullAuthenticator(), connectionManager);
                            break;

                        default:
                            deviceScopeIdentitiesCache = await c.Resolve<Task<IDeviceScopeIdentitiesCache>>();
                            IAuthenticator cloudAuthenticator = new CloudTokenAuthenticator(connectionManager);
                            tokenAuthenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, this.iothubHostName, this.edgeDeviceHostName, cloudAuthenticator, connectionManager);
                            break;
                    }

                    return new Authenticator(tokenAuthenticator, this.edgeDeviceId, connectionManager) as IAuthenticator;
                })
                .As<Task<IAuthenticator>>()
                .SingleInstance();

            // IClientCredentialsFactory
            builder.Register(c => new ClientCredentialsFactory(this.iothubHostName, this.productInfo))
                .As<IClientCredentialsFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
