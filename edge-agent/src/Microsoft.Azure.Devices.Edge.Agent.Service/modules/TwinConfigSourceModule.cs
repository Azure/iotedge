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

    public class TwinConfigSourceModule : Module
    {
        readonly DeviceClientModule deviceClientModule;
        const string DockerType = "docker";

        public TwinConfigSourceModule(string connectionString)
        {
            this.deviceClientModule = new DeviceClientModule(
                Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString))
            );
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(this.deviceClientModule);

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
                    c =>
                    {
                        var dockerFactory = new TwinReportStateCommandFactory(
                            new DockerCommandFactory(c.Resolve<IDockerClient>()),
                            c.Resolve<IDeviceClient>(),
                            c.Resolve<IEnvironment>()
                        );
                        return new LoggingCommandFactory(dockerFactory, c.Resolve<ILoggerFactory>());
                    })
                .As<ICommandFactory>()
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
                        IConfigSource config = await TwinConfigSource.Create(
                            c.Resolve<IDeviceClient>(),
                            c.Resolve<ISerde<ModuleSet>>(),
                            c.Resolve<ISerde<Diff>>(),
                            c.Resolve<ILoggerFactory>()
                        );
                        return config;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            base.Load(builder);
        }

    }
}