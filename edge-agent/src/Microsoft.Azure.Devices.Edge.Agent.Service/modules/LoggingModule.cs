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
        public LoggingModule()
        {
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ILoggerFactory
            builder.Register(c => Logger.Factory)
                .As<ILoggerFactory>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
