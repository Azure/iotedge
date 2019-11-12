// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class FileConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string configFilename;
        readonly IConfiguration configuration;
        readonly VersionInfo versionInfo;

        public FileConfigSourceModule(
            string configFilename,
            IConfiguration configuration,
            VersionInfo versionInfo)
        {
            this.configFilename = Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename));
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.versionInfo = Preconditions.CheckNotNull(versionInfo, nameof(versionInfo));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        var serde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        IConfigSource config = await FileConfigSource.Create(
                            this.configFilename,
                            this.configuration,
                            serde);
                        return config;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // ISdkModuleClientProvider
            builder.Register(c => new SdkModuleClientProvider())
                .As<ISdkModuleClientProvider>()
                .SingleInstance();

            // IEdgeAgentConnection
            builder.Register(
                c =>
                {
                    var serde = c.Resolve<ISerde<DeploymentConfig>>();
                    var deviceClientprovider = c.Resolve<IModuleClientProvider>();
                    var requestManager = c.Resolve<IRequestManager>();
                    var deviceManager = c.Resolve<IDeviceManager>();
                    IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(deviceClientprovider, serde, requestManager, deviceManager);
                    return edgeAgentConnection;
                })
                .As<IEdgeAgentConnection>()
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

                    var edgeAgentConnection = c.Resolve<IEdgeAgentConnection>();
                    return new IoTHubReporter(
                        edgeAgentConnection,
                        new TypeSpecificSerDe<AgentState>(deserializerTypesMap),
                        this.versionInfo) as IReporter;
                })
                .As<IReporter>()
                .SingleInstance();

            // IRequestManager
            builder.Register(
                    c => new RequestManager(Enumerable.Empty<IRequestHandler>(), TimeSpan.Zero) as IRequestManager)
                .As<IRequestManager>()
                .SingleInstance();

            // Task<IStreamRequestListener>
            builder.Register(c => Task.FromResult(new NullStreamRequestListener() as IStreamRequestListener))
                .As<Task<IStreamRequestListener>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
