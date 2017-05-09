// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using Autofac;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;

    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            Logger loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] - {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            // ILoggerFactory
            builder.Register(c => new LoggerFactory().AddSerilog(loggerConfig))
                .As<ILoggerFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}