// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LoggingModule : Module
    {
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