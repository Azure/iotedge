// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
	using Microsoft.Extensions.Configuration;

	public class FileBackupConfigSourceModule : Module
    {
        readonly string connectionString;
        readonly string backupConfigFilePath;
        const string DockerType = "docker";
		readonly IConfiguration configuration;

        public FileBackupConfigSourceModule(string connectionString, string backupConfigFilePath, IConfiguration config)
        {
            this.connectionString = Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            this.backupConfigFilePath = Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath));
			this.configuration = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new DeviceClientModule(this.connectionString));

            // ISerde<Diff>
            builder.Register(c => new DiffSerde(
                    new Dictionary<string, Type>
                    {
                        { DockerType, typeof(DockerModule) }
                    }
                ))
                .As<ISerde<Diff>>()
                .SingleInstance();

            // ICommandFactory
            builder.Register(
                    async c =>
                    {
                        var dockerFactory = new TwinReportStateCommandFactory(
                            new DockerCommandFactory(
                                c.Resolve<IDockerClient>(),
                                c.Resolve<DockerLoggingConfig>(),
                                await c.Resolve<Task<IConfigSource>>()),
                            c.Resolve<IDeviceClient>(),
                            c.Resolve<IEnvironment>()
                        );
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

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
						ISerde<ModuleSet> moduleSetSerde = c.Resolve<ISerde<ModuleSet>>();
                        IConfigSource twinConfigSource = await TwinConfigSource.Create(
                            c.Resolve<IDeviceClient>(),
                            moduleSetSerde,
                            c.Resolve<ISerde<Diff>>(),
							this.configuration
                        );
                        IConfigSource backupConfigSource = new FileBackupConfigSource(this.backupConfigFilePath, moduleSetSerde, twinConfigSource, this.configuration);
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            base.Load(builder);
        }

    }
}