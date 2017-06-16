// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;

    public class LoggingModule : Module
    {
        readonly string dockerLoggingDriver;

        public LoggingModule(string dockerLoggingDriver)
        {
            this.dockerLoggingDriver = Preconditions.CheckNotNull(dockerLoggingDriver, nameof(dockerLoggingDriver));
        }

        protected override void Load(ContainerBuilder builder)
        {
            Serilog.Core.Logger loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] - {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            // ILoggerFactory
            builder.Register(c => new LoggerFactory().AddSerilog(loggerConfig))
                .As<ILoggerFactory>()
                .SingleInstance();

            // DockerLoggingConfig
            builder.Register(c => new DockerLoggingConfig(this.dockerLoggingDriver))
                .As<DockerLoggingConfig>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}