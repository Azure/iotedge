// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    public class AgentModule : Module
    {
        const string DockerType = "docker";
        readonly int maxRestartCount;
        readonly TimeSpan intensiveCareTime;
        readonly int coolOffTimeUnitInSeconds;
        readonly bool usePersistentStorage;
        readonly string storagePath;
        readonly Option<Uri> workloadUri;
        readonly Option<string> workloadApiVersion;
        readonly string moduleId;
        readonly Option<string> moduleGenerationId;
        readonly bool useBackupAndRestore;
        readonly Option<string> storageBackupPath;
        readonly Option<ulong> storageTotalMaxWalSize;

        public AgentModule(int maxRestartCount, TimeSpan intensiveCareTime, int coolOffTimeUnitInSeconds, bool usePersistentStorage, string storagePath, bool useBackupAndRestore, Option<string> storageBackupPath, Option<ulong> storageTotalMaxWalSize)
            : this(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.None<Uri>(), Option.None<string>(), Constants.EdgeAgentModuleIdentityName, Option.None<string>(), useBackupAndRestore, storageBackupPath, storageTotalMaxWalSize)
        {
        }

        public AgentModule(
            int maxRestartCount,
            TimeSpan intensiveCareTime,
            int coolOffTimeUnitInSeconds,
            bool usePersistentStorage,
            string storagePath,
            Option<Uri> workloadUri,
            Option<string> workloadApiVersion,
            string moduleId,
            Option<string> moduleGenerationId,
            bool useBackupAndRestore,
            Option<string> storageBackupPath,
            Option<ulong> storageTotalMaxWalSize)
        {
            this.maxRestartCount = maxRestartCount;
            this.intensiveCareTime = intensiveCareTime;
            this.coolOffTimeUnitInSeconds = coolOffTimeUnitInSeconds;
            this.usePersistentStorage = usePersistentStorage;
            this.storagePath = Preconditions.CheckNonWhiteSpace(storagePath, nameof(storagePath));
            this.workloadUri = workloadUri;
            this.workloadApiVersion = workloadApiVersion;
            this.moduleId = moduleId;
            this.moduleGenerationId = moduleGenerationId;
            this.useBackupAndRestore = useBackupAndRestore;
            this.storageBackupPath = storageBackupPath;
            this.storageTotalMaxWalSize = storageTotalMaxWalSize;
        }

        static Dictionary<Type, IDictionary<string, Type>> DeploymentConfigTypeMapping
        {
            get
            {
                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    [DockerType] = typeof(DockerDesiredModule)
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    [DockerType] = typeof(EdgeAgentDockerModule)
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    [DockerType] = typeof(EdgeHubDockerModule)
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    [DockerType] = typeof(DockerRuntimeInfo)
                };

                var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };
                return deserializerTypesMap;
            }
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ISerde<ModuleSet>
            builder.Register(
                    c => new ModuleSetSerde(
                        new Dictionary<string, Type>
                        {
                            { DockerType, typeof(DockerModule) }
                        }))
                .As<ISerde<ModuleSet>>()
                .SingleInstance();

            // ISerde<DeploymentConfig>
            builder.Register(
                    c =>
                    {
                        ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(DeploymentConfigTypeMapping);
                        return serde;
                    })
                .As<ISerde<DeploymentConfig>>()
                .SingleInstance();

            // ISerde<DeploymentConfigInfo>
            builder.Register(
                    c =>
                    {
                        ISerde<DeploymentConfigInfo> serde = new TypeSpecificSerDe<DeploymentConfigInfo>(DeploymentConfigTypeMapping);
                        return serde;
                    })
                .As<ISerde<DeploymentConfigInfo>>()
                .SingleInstance();

            // Detect system environment
            builder.Register(c => new SystemEnvironment())
                .As<ISystemEnvironment>()
                .SingleInstance();

            // IRocksDbOptionsProvider
            // For EdgeAgent, we don't need high performance from RocksDb, so always turn off optimizeForPerformance
            builder.Register(c => new RocksDbOptionsProvider(c.Resolve<ISystemEnvironment>(), false, this.storageTotalMaxWalSize))
                .As<IRocksDbOptionsProvider>()
                .SingleInstance();

            if (!this.usePersistentStorage && this.useBackupAndRestore)
            {
                // Backup and restore serialization
                builder.Register(c => new ProtoBufDataBackupRestore())
                    .As<IDataBackupRestore>()
                    .SingleInstance();
            }

            // IDbStoreProvider
            builder.Register(
                    async c =>
                    {
                        var loggerFactory = c.Resolve<ILoggerFactory>();
                        ILogger logger = loggerFactory.CreateLogger(typeof(AgentModule));

                        if (this.usePersistentStorage)
                        {
                            // Create partition for mma
                            var partitionsList = new List<string> { "moduleState", "deploymentConfig" };
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
                                IDbStoreProvider dbStoreProvider = await this.BuildInMemoryDbStoreProvider(c);
                                return dbStoreProvider;
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Using in-memory store");
                            IDbStoreProvider dbStoreProvider = await this.BuildInMemoryDbStoreProvider(c);
                            return dbStoreProvider;
                        }
                    })
                .As<Task<IDbStoreProvider>>()
                .SingleInstance();

            // Task<IStoreProvider>
            builder.Register(async c =>
            {
                var dbStoreProvider = await c.Resolve<Task<IDbStoreProvider>>();
                IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
                return storeProvider;
            })
            .As<Task<IStoreProvider>>()
            .SingleInstance();

            // IEntityStore<string, ModuleState>
            builder.Register(async c =>
                {
                IStoreProvider storeProvider = await c.Resolve<Task<IStoreProvider>>();
                return storeProvider.GetEntityStore<string, ModuleState>("moduleState");
                })
                .As<Task<IEntityStore<string, ModuleState>>>()
                .SingleInstance();

            // IEntityStore<string, DeploymentConfigInfo>
            builder.Register(async c =>
                {
                IStoreProvider storeProvider = await c.Resolve<Task<IStoreProvider>>();
                return storeProvider.GetEntityStore<string, string>("deploymentConfig");
                })
                .As<Task<IEntityStore<string, string>>>()
                .SingleInstance();

            // IRestartManager
            builder.Register(c => new RestartPolicyManager(this.maxRestartCount, this.coolOffTimeUnitInSeconds))
                .As<IRestartPolicyManager>()
                .SingleInstance();

            // IPlanner
            builder.Register(
                    async c => new HealthRestartPlanner(
                        await c.Resolve<Task<ICommandFactory>>(),
                        await c.Resolve<Task<IEntityStore<string, ModuleState>>>(),
                        this.intensiveCareTime,
                        c.Resolve<IRestartPolicyManager>()) as IPlanner)
                .As<Task<IPlanner>>()
                .SingleInstance();

            // IPlanRunner
            builder.Register(c => new OrderedRetryPlanRunner(this.maxRestartCount, this.coolOffTimeUnitInSeconds, SystemTime.Instance))
                .As<IPlanRunner>()
                .SingleInstance();

            // IEncryptionDecryptionProvider
            builder.Register(
                    async c =>
                    {
                        IEncryptionProvider provider = await this.workloadUri.Map(
                            async uri =>
                            {
                                IEncryptionProvider encryptionProvider = await EncryptionProvider.CreateAsync(
                                    this.storagePath,
                                    uri,
                                    this.workloadApiVersion.Expect(() => new InvalidOperationException("Missing workload API version")),
                                    Constants.EdgeletClientApiVersion,
                                    this.moduleId,
                                    this.moduleGenerationId.Expect(() => new InvalidOperationException("Missing generation ID")),
                                    Constants.EdgeletInitializationVectorFileName);
                                return encryptionProvider;
                            }).GetOrElse(() => Task.FromResult<IEncryptionProvider>(NullEncryptionProvider.Instance));

                        return provider;
                    })
                .As<Task<IEncryptionProvider>>()
                .SingleInstance();

            // IAvailabilityMetric
            builder.Register(c => new AvailabilityMetrics(c.Resolve<IMetricsProvider>(), this.storagePath))
                .As<IAvailabilityMetric>()
                .SingleInstance();

            // Task<Agent>
            builder.Register(
                    async c =>
                    {
                        var configSource = c.Resolve<Task<IConfigSource>>();
                        var environmentProvider = c.Resolve<Task<IEnvironmentProvider>>();
                        var planner = c.Resolve<Task<IPlanner>>();
                        var planRunner = c.Resolve<IPlanRunner>();
                        var reporter = c.Resolve<IReporter>();
                        var moduleIdentityLifecycleManager = c.Resolve<IModuleIdentityLifecycleManager>();
                        var deploymentConfigInfoSerde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        var deploymentConfigInfoStore = await c.Resolve<Task<IEntityStore<string, string>>>();
                        var encryptionProvider = c.Resolve<Task<IEncryptionProvider>>();
                        var availabilityMetric = c.Resolve<IAvailabilityMetric>();
                        return await Agent.Create(
                            await configSource,
                            await planner,
                            planRunner,
                            reporter,
                            moduleIdentityLifecycleManager,
                            await environmentProvider,
                            deploymentConfigInfoStore,
                            deploymentConfigInfoSerde,
                            await encryptionProvider,
                            availabilityMetric);
                    })
                .As<Task<Agent>>()
                .SingleInstance();

            base.Load(builder);
        }

        async Task<IDbStoreProvider> BuildInMemoryDbStoreProvider(IComponentContext container)
        {
            IDbStoreProvider dbStoreProvider = DbStoreProviderFactory.GetInMemoryDbStore(Option.None<IStorageSpaceChecker>());
            if (this.useBackupAndRestore)
            {
                var backupRestore = container.Resolve<IDataBackupRestore>();

                string backupPathValue = this.storageBackupPath.Expect(() => new InvalidOperationException("Storage backup path missing"));
                dbStoreProvider = await dbStoreProvider.WithBackupRestore(backupPathValue, backupRestore);
            }

            return dbStoreProvider;
        }
    }
}
