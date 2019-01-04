// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System.Collections.Generic;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LoggingModule : Module
    {
        readonly string dockerLoggingDriver;
        readonly IDictionary<string, string> driverOptions;

        public LoggingModule(string dockerLoggingDriver, IDictionary<string, string> loggingDriverOptions)
        {
            this.dockerLoggingDriver = Preconditions.CheckNotNull(dockerLoggingDriver, nameof(dockerLoggingDriver));
            this.driverOptions = Preconditions.CheckNotNull(loggingDriverOptions, nameof(loggingDriverOptions));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ILoggerFactory
            builder.Register(c => Logger.Factory)
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
