// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System.Collections;
    using System.Collections.Generic;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;

    public class LoggingModule : Module
    {
        readonly string dockerLoggingDriver;
        readonly IDictionary<string, string> driverOptions;

        public LoggingModule(string dockerLoggingDriver, IDictionary<string,string> loggingDriverOptions)
        {
            this.dockerLoggingDriver = Preconditions.CheckNotNull(dockerLoggingDriver, nameof(dockerLoggingDriver));
            this.driverOptions = Preconditions.CheckNotNull(loggingDriverOptions, nameof(loggingDriverOptions));
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
            builder.Register(c => new DockerLoggingConfig(this.dockerLoggingDriver, this.driverOptions))
                .As<DockerLoggingConfig>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}