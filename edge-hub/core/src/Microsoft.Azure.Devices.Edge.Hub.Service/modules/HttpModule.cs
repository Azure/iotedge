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
        readonly string edgeDeviceId;
        readonly string proxyModuleId;

        public HttpModule(string iothubHostName, string edgeDeviceId, string proxyModuleId)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.proxyModuleId = Preconditions.CheckNonWhiteSpace(proxyModuleId, nameof(proxyModuleId));
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

            // IHttpProxiedCertificateExtractor
            builder.Register(
                async c =>
                {
                    var authenticator = await c.Resolve<Task<IAuthenticator>>();
                    var credFactory = c.Resolve<IClientCredentialsFactory>();
                    IHttpProxiedCertificateExtractor httpProxiedCertificateExtractor = new HttpProxiedCertificateExtractor(authenticator, credFactory, this.iothubHostName, this.edgeDeviceId, this.proxyModuleId);
                    return httpProxiedCertificateExtractor;
                })
                .As<Task<IHttpProxiedCertificateExtractor>>()
                .SingleInstance();

            // IHttpAuthenticator
            builder.Register(
                async c =>
                {
                    var authenticator = await c.Resolve<Task<IAuthenticator>>();
                    var credFactory = c.Resolve<IClientCredentialsFactory>();
                    var httpProxiedCertificateExtractor = await c.Resolve<Task<IHttpProxiedCertificateExtractor>>();
                    IHttpRequestAuthenticator httpAuthenticator = new HttpRequestAuthenticator(authenticator, credFactory, this.iothubHostName, httpProxiedCertificateExtractor);
                    return httpAuthenticator;
                })
                .As<Task<IHttpRequestAuthenticator>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
