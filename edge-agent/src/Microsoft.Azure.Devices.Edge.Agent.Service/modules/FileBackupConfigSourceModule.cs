// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileBackupConfigSourceModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;
        readonly string edgeDeviceConnectionString;
        readonly string backupConfigFilePath;
        const string DockerType = "docker";
        const string UnknownType = "Unknown";
        readonly IConfiguration configuration;

        public FileBackupConfigSourceModule(EdgeHubConnectionString connectionDetails, string edgeDeviceConnectionString, string backupConfigFilePath, IConfiguration config)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails));
            this.edgeDeviceConnectionString = Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString));
            this.backupConfigFilePath = Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath));
            this.configuration = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new DeviceClientModule(this.connectionDetails));
            builder.RegisterModule(new ServiceClientModule(this.connectionDetails, this.edgeDeviceConnectionString));

            // ISerde<Diff>
            builder.Register(c => new DiffSerde(
                    new Dictionary<string, Type>
                    {
                        { DockerType, typeof(DockerModule) }
                    }
                ))
                .As<ISerde<Diff>>()
                .SingleInstance();

            // IModuleIdentityLifecycleManager
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IServiceClient>(), this.connectionDetails))
                .As<IModuleIdentityLifecycleManager>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerFactory = new DockerCommandFactory(
                                c.Resolve<IDockerClient>(),
                                c.Resolve<DockerLoggingConfig>(),
                                await c.Resolve<Task<IConfigSource>>());
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

            // ISerde<ModuleSet>
            builder.Register(c => new ModuleSetSerde(
                    new Dictionary<string, Type>
                    {
                        { DockerType, typeof(DockerModule) }
                    }
                ))
                .As<ISerde<ModuleSet>>()
                .SingleInstance();

            // Task<IEdgeAgentConnection>
            builder.Register(
                async c =>
                {
                    ISerde<DeploymentConfig> serde = c.Resolve<ISerde<DeploymentConfig>>();
                    IDeviceClient deviceClient = await c.Resolve<Task<IDeviceClient>>();
                    IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
                    return edgeAgentConnection;
                })
                .As<Task<IEdgeAgentConnection>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        ISerde<DeploymentConfigInfo> serde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        IEdgeAgentConnection edgeAgentConnection = await c.Resolve<Task<IEdgeAgentConnection>>();
                        var twinConfigSource = new TwinConfigSource(edgeAgentConnection, this.configuration);
                        IConfigSource backupConfigSource = new FileBackupConfigSource(this.backupConfigFilePath, twinConfigSource, serde);
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // Task<IReporter>
            builder.Register(
                    async c =>
                    {
                        var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(DockerReportedRuntimeInfo)
                        };

                        var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeAgentDockerRuntimeModule)
                        };

                        var edgeHubDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeHubDockerRuntimeModule),
                            [UnknownType] = typeof(UnknownEdgeHubModule)
                        };

                        var moduleDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(DockerRuntimeModule)
                        };

                        var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
                        {
                            { typeof(IRuntimeInfo), runtimeInfoDeserializerTypes },
                            { typeof(IEdgeAgentModule), edgeAgentDeserializerTypes },
                            { typeof(IEdgeHubModule), edgeHubDeserializerTypes },
                            { typeof(IModule), moduleDeserializerTypes }
                        };

                        return new IoTHubReporter(
                            await c.Resolve<Task<IEdgeAgentConnection>>(),
                            await c.Resolve<Task<IEnvironment>>(),
                            new TypeSpecificSerDe<AgentState>(deserializerTypesMap)
                        ) as IReporter;
                    }
                )
                .As<Task<IReporter>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
