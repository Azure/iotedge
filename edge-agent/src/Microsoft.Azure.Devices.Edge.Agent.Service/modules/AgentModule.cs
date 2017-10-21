// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;

    public class AgentModule : Module
    {
        readonly Uri dockerHostname;
        readonly int maxRestartCount;
        readonly TimeSpan intensiveCareTime;
        readonly int coolOffTimeUnitInSeconds;
        const string DockerType = "docker";

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

        public AgentModule(Uri dockerHostname, int maxRestartCount, TimeSpan intensiveCareTime, int coolOffTimeUnitInSeconds)
        {
            this.dockerHostname = Preconditions.CheckNotNull(dockerHostname, nameof(dockerHostname));
            this.maxRestartCount = maxRestartCount;
            this.intensiveCareTime = intensiveCareTime;
            this.coolOffTimeUnitInSeconds = coolOffTimeUnitInSeconds;
        }

        protected override void Load(ContainerBuilder builder)
        {
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

            // IDockerClient
            builder.Register(c => new DockerClientConfiguration(this.dockerHostname).CreateClient())
                .As<IDockerClient>()
                .SingleInstance();

            // IDbStore
            builder.Register(
                c =>
                {
                    // Create partition for mma
                    var partitionsList = new List<string> { Constants.MMAStorePartitionKey };

                    return new InMemoryDbStoreProvider();
                    //return Storage.RocksDb.DbStoreProvider.Create(this.storeAndForwardConfiguration.StoragePath, partitionsList);
                })
                .As<IDbStoreProvider>()
                .SingleInstance();

            // IStoreProvider
            builder.Register(c => new StoreProvider(c.Resolve<IDbStoreProvider>()))
                .As<IStoreProvider>()
                .SingleInstance();

            // IEntityStore<string, RestartState>
            builder.Register(c => c.Resolve<IStoreProvider>().GetEntityStore<string, ModuleState>(Constants.MMAStorePartitionKey))
                .As<IEntityStore<string, ModuleState>>()
                .SingleInstance();

            // IEnvironment
            builder.Register(
                async c =>
                {
                    IEnvironment dockerEnvironment = await DockerEnvironment.CreateAsync(
                        c.Resolve<IDockerClient>(),
                        c.Resolve<IEntityStore<string, ModuleState>>(),
                        c.Resolve<IRestartPolicyManager>());
                    return dockerEnvironment;
                })
             .As<Task<IEnvironment>>()
             .SingleInstance();

            // IRestartManager
            builder.Register(c => new RestartPolicyManager(this.maxRestartCount, this.coolOffTimeUnitInSeconds))
                .As<IRestartPolicyManager>()
                .SingleInstance();

            // IPlanner
            builder.Register(async c => new HealthRestartPlanner(
                    await c.Resolve<Task<ICommandFactory>>(),
                    c.Resolve<IEntityStore<string, ModuleState>>(),
                    this.intensiveCareTime,
                    c.Resolve<IRestartPolicyManager>()
                ) as IPlanner)
                .As<Task<IPlanner>>()
                .SingleInstance();

            // Task<Agent>
            builder.Register(
                async c =>
                {
                    var configSource = c.Resolve<Task<IConfigSource>>();
                    var environment = c.Resolve<Task<IEnvironment>>();
                    var planner = c.Resolve<Task<IPlanner>>();
                    var reporter = c.Resolve<Task<IReporter>>();
                    var moduleIdentityLifecycleManager = c.Resolve<IModuleIdentityLifecycleManager>();
                    return new Agent(
                        await configSource,
                        await environment,
                        await planner,
                        await reporter,
                        moduleIdentityLifecycleManager);
                })
                .As<Task<Agent>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
