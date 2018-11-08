// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
