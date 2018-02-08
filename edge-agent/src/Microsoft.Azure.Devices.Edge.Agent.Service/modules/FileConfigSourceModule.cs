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
    using Microsoft.Azure.Devices.Edge.Agent.Core.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string configFilename;
        readonly IConfiguration configuration;
        readonly EdgeHubConnectionString connectionDetails;
        readonly string edgeDeviceConnectionString;

        public FileConfigSourceModule(string configFilename,
            IConfiguration configuration,
            EdgeHubConnectionString connectionDetails,
            string edgeDeviceConnectionString)
        {
            this.configFilename = Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename));
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.connectionDetails = Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails));
            this.edgeDeviceConnectionString = Preconditions.CheckNonWhiteSpace(edgeDeviceConnectionString, nameof(edgeDeviceConnectionString));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new ServiceClientModule(this.connectionDetails, this.edgeDeviceConnectionString));

            // IModuleIdentityLifecycleManager
            builder.Register(c => new ModuleIdentityLifecycleManager(c.Resolve<IServiceClient>(), this.connectionDetails))
                .As<IModuleIdentityLifecycleManager>()
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

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerFactory = new DockerCommandFactory(
                            c.Resolve<IDockerClient>(),
                            c.Resolve<DockerLoggingConfig>(),
                            await c.Resolve<Task<IConfigSource>>());
                        // Task<Interface> cannot be assigned to with Task<DerivedType>, so return as ICommandFactory.
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>()) as ICommandFactory;
                    })
                .As<Task<ICommandFactory>>()
                .SingleInstance();

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

            // Task<IReporter>
            // TODO: When using a file backed config source we need to figure out
            // how reporting will work.
            builder.Register(c => Task.FromResult(NullReporter.Instance as IReporter))
                .As<Task<IReporter>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
