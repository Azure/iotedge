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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public class ConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string configFilename;

        public ConfigSourceModule(string configFilename)
        {
            this.configFilename = Preconditions.CheckNonWhiteSpace(configFilename, nameof(configFilename));
        }

        protected override void Load(ContainerBuilder builder)
        {
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
                    IConfigSource config = await FileConfigSource.Create(this.configFilename, c.Resolve<ISerde<ModuleSet>>(), c.Resolve<ILoggerFactory>());
                    return config;
                })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}