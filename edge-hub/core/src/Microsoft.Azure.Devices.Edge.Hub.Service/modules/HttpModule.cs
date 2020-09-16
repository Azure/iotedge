// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;

    public class HttpModule : Module
    {
        readonly string iothubHostName;

        public HttpModule(string iothubHostName)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
        }

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

            // IHttpAuthenticator
            builder.Register(
                async c =>
                {
                    var authenticator = await c.Resolve<Task<IAuthenticator>>();
                    var credFactory = c.Resolve<IClientCredentialsFactory>();
                    IHttpRequestAuthenticator httpAuthenticator = new HttpRequestAuthenticator(authenticator, credFactory, this.iothubHostName);
                    return httpAuthenticator;
                })
                .As<Task<IHttpRequestAuthenticator>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
