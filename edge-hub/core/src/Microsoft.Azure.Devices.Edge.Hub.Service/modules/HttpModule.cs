// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;

    public class HttpModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // IValidator
            builder.Register(c => new MethodRequestValidator())
                .As<IValidator<MethodRequest>>()
                .SingleInstance();

            // IWebSocketListenerRegistry
            builder.Register(c => new WebSocketListenerRegistry())
                .As<IWebSocketListenerRegistry>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
