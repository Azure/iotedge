// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
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
        readonly string storageBackupPath;

        public AgentModule(int maxRestartCount, TimeSpan intensiveCareTime, int coolOffTimeUnitInSeconds, bool usePersistentStorage, string storagePath, bool useBackupAndRestore, string storageBackupPath)
            : this(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.None<Uri>(), Option.None<string>(), Constants.EdgeAgentModuleIdentityName, Option.None<string>(), useBackupAndRestore, storageBackupPath)
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
            string storageBackupPath)
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
            builder.Register(c => new RocksDbOptionsProvider(c.Resolve<ISystemEnvironment>(), false))
                .As<IRocksDbOptionsProvider>()
                .SingleInstance();

            // IDbStore
            builder.Register(
                    c =>
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

            // IStoreProvider
            builder.Register(c => new StoreProvider(c.Resolve<IDbStoreProvider>()))
                .As<IStoreProvider>()
                .SingleInstance();

            // IEntityStore<string, ModuleState>
            builder.Register(c => c.Resolve<IStoreProvider>().GetEntityStore<string, ModuleState>("moduleState"))
                .As<IEntityStore<string, ModuleState>>()
                .SingleInstance();

            // IEntityStore<string, DeploymentConfigInfo>
            builder.Register(c => c.Resolve<IStoreProvider>().GetEntityStore<string, string>("deploymentConfig"))
                .As<IEntityStore<string, string>>()
                .SingleInstance();

            // IRestartManager
            builder.Register(c => new RestartPolicyManager(this.maxRestartCount, this.coolOffTimeUnitInSeconds))
                .As<IRestartPolicyManager>()
                .SingleInstance();

            // IPlanner
            builder.Register(
                    async c => new HealthRestartPlanner(
                        await c.Resolve<Task<ICommandFactory>>(),
                        c.Resolve<IEntityStore<string, ModuleState>>(),
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
                        var deploymentConfigInfoStore = c.Resolve<IEntityStore<string, string>>();
                        var encryptionProvider = c.Resolve<Task<IEncryptionProvider>>();
                        return await Agent.Create(
                            await configSource,
                            await planner,
                            planRunner,
                            reporter,
                            moduleIdentityLifecycleManager,
                            await environmentProvider,
                            deploymentConfigInfoStore,
                            deploymentConfigInfoSerde,
                            await encryptionProvider);
                    })
                .As<Task<Agent>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
