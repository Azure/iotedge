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

                    IDeviceClient deviceClient = await c.Resolve<Task<IDeviceClient>>();
                    ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypesMap);
                    IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
                    return edgeAgentConnection;
                })
                .As<Task<IEdgeAgentConnection>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        IEdgeAgentConnection edgeAgentConnection = await c.Resolve<Task<IEdgeAgentConnection>>();
                        IConfigSource twinConfigSource = new TwinConfigSource(edgeAgentConnection, this.configuration);
                        IConfigSource backupConfigSource = new FileBackupConfigSource(this.backupConfigFilePath, twinConfigSource);
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // Task<IReporter>
            builder.Register(
                    async c => new IoTHubReporter(
                        await c.Resolve<Task<IEdgeAgentConnection>>(),
                        await c.Resolve<Task<IEnvironment>>()
                    ) as IReporter
                )
                .As<Task<IReporter>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
