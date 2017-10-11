// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;

    public class FileBackupConfigSourceModule : Module
    {
        readonly EdgeHubConnectionString connectionDetails;
        readonly string backupConfigFilePath;
        const string DockerType = "docker";
        readonly IConfiguration configuration;

        public FileBackupConfigSourceModule(EdgeHubConnectionString connectionStringBuilder, string backupConfigFilePath, IConfiguration config)
        {
            this.connectionDetails = Preconditions.CheckNotNull(connectionStringBuilder, nameof(connectionStringBuilder));
            this.backupConfigFilePath = Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath));
            this.configuration = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new DeviceClientModule(this.connectionDetails));
            builder.RegisterModule(new ServiceClientModule(this.connectionDetails));

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

            // Task<ITwinConfigSource>
            builder.Register(
                    async c =>
                    {
                        ISerde<ModuleSet> moduleSetSerde = c.Resolve<ISerde<ModuleSet>>();
                        Task<IDeviceClient> deviceClientTask = c.Resolve<Task<IDeviceClient>>();
                        ISerde<Diff> diff = c.Resolve<ISerde<Diff>>();
                        ITwinConfigSource twinConfigSource = await TwinConfigSource.Create(
                            await deviceClientTask,
                            moduleSetSerde,
                            diff,
							this.configuration
                        );
                        return twinConfigSource;
                    })
                .As<Task<ITwinConfigSource>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        ISerde<ModuleSet> moduleSetSerde = c.Resolve<ISerde<ModuleSet>>();
                        IConfigSource backupConfigSource = new FileBackupConfigSource(
                            this.backupConfigFilePath,
                            moduleSetSerde,
                            await c.Resolve<Task<ITwinConfigSource>>(),
                            this.configuration
                        );
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // Task<IReporter>
            builder.Register(
                    async c =>
                    {
                        Task<IDeviceClient> deviceTask = c.Resolve<Task<IDeviceClient>>();
                        Task<ITwinConfigSource> twinTask = c.Resolve<Task<ITwinConfigSource>>();
                        return new IoTHubReporter(
                            await deviceTask,
                            await twinTask) as IReporter;
                    })
                .As<Task<IReporter>>()
                .SingleInstance();

            base.Load(builder);
        }

    }
}
