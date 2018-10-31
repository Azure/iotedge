// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Util;

    public class TwinConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly IAgentAppSettings appSettings;

        public TwinConfigSourceModule(
            IAgentAppSettings appSettings
        )
        {
            this.appSettings = Preconditions.CheckNotNull(appSettings, nameof(appSettings));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IEdgeAgentConnection
            builder.Register(
                    c =>
                    {
                        var serde = c.Resolve<ISerde<DeploymentConfig>>();
                        var deviceClientprovider = c.Resolve<IModuleClientProvider>();
                        IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(deviceClientprovider, serde, this.appSettings.CoolOffTimeUnit);
                        return edgeAgentConnection;
                    })
                .As<IEdgeAgentConnection>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        var serde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        var edgeAgentConnection = c.Resolve<IEdgeAgentConnection>();
                        IEncryptionProvider encryptionProvider = await c.Resolve<Task<IEncryptionProvider>>();
                        var twinConfigSource = new TwinConfigSource(edgeAgentConnection, this.appSettings);
                        IConfigSource backupConfigSource = new FileBackupConfigSource(this.appSettings.BackupConfigFilePath, twinConfigSource, serde, encryptionProvider);
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // IReporter
            builder.Register(
                    c =>
                    {
                        var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(DockerReportedRuntimeInfo),
                            [Constants.Unknown] = typeof(UnknownRuntimeInfo)
                        };

                        var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeAgentDockerRuntimeModule),
                            [Constants.Unknown] = typeof(UnknownEdgeAgentModule)
                        };

                        var edgeHubDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeHubDockerRuntimeModule),
                            [Constants.Unknown] = typeof(UnknownEdgeHubModule)
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
                            c.Resolve<IEdgeAgentConnection>(),
                            new TypeSpecificSerDe<AgentState>(deserializerTypesMap),
                            this.appSettings.VersionInfo
                        ) as IReporter;
                    }
                )
                .As<IReporter>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
