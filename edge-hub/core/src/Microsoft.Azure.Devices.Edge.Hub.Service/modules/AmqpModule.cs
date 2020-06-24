// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AmqpModule : Module
    {
        // Azure AMQP Library needs HostName to figure out the IP Addresses on
        // which to listen to for incoming connections. If you pass in the
        // fully qualified hostname or "localhost" it uses IPAddress.IPV6Any (which is what we want)
        // However, the code to get the fully qualified hostname - Dns.GetHostEntry(string.Empty).HostName
        // does not work on Linux. So hardcode HostName for AMQP to "localhost"
        const string HostName = "localhost";
        readonly string scheme;
        readonly int port;
        readonly X509Certificate2 tlsCertificate;
        readonly string iotHubHostName;
        readonly bool clientCertAuthAllowed;
        readonly SslProtocols sslProtocols;

        public AmqpModule(
            string scheme,
            int port,
            X509Certificate2 tlsCertificate,
            string iotHubHostName,
            bool clientCertAuthAllowed,
            SslProtocols sslProtocols)
        {
            this.scheme = Preconditions.CheckNonWhiteSpace(scheme, nameof(scheme));
            this.port = Preconditions.CheckRange(port, 0, ushort.MaxValue, nameof(port));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.sslProtocols = sslProtocols;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ITransportSettings
            builder.Register(
                    async c =>
                    {
                        IClientCredentialsFactory clientCredentialsProvider = c.Resolve<IClientCredentialsFactory>();
                        IAuthenticator authenticator = await c.Resolve<Task<IAuthenticator>>();
                        ITransportSettings settings = new DefaultTransportSettings(this.scheme, HostName, this.port, this.tlsCertificate, this.clientCertAuthAllowed, authenticator, clientCredentialsProvider, this.sslProtocols);
                        return settings;
                    })
                .As<Task<ITransportSettings>>()
                .SingleInstance();

            // ITransportListenerProvider
            builder.Register(c => new AmqpTransportListenerProvider())
                .As<ITransportListenerProvider>()
                .SingleInstance();

            // Task<ILinkHandlerProvider>
            builder.Register(
                    async c =>
                    {
                        IMessageConverter<AmqpMessage> messageConverter = new AmqpMessageConverter();
                        IMessageConverter<AmqpMessage> twinMessageConverter = new AmqpTwinMessageConverter();
                        IMessageConverter<AmqpMessage> directMethodMessageConverter = new AmqpDirectMethodMessageConverter();
                        var identityProvider = c.Resolve<IIdentityProvider>();
                        var productInfoStore = await c.Resolve<Task<IProductInfoStore>>();
                        ILinkHandlerProvider linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, directMethodMessageConverter, identityProvider, productInfoStore);
                        return linkHandlerProvider;
                    })
                .As<Task<ILinkHandlerProvider>>()
                .SingleInstance();

            // Task<AmqpProtocolHead>
            builder.Register(
                    async c =>
                    {
                        var identityFactory = c.Resolve<IClientCredentialsFactory>();
                        var transportSettingsTask = c.Resolve<Task<ITransportSettings>>();
                        var transportListenerProvider = c.Resolve<ITransportListenerProvider>();
                        var linkHandlerProvider = await c.Resolve<Task<ILinkHandlerProvider>>();
                        var credentialsCacheTask = c.Resolve<Task<ICredentialsCache>>();
                        var authenticatorTask = c.Resolve<Task<IAuthenticator>>();
                        var connectionProviderTask = c.Resolve<Task<IConnectionProvider>>();
                        ICredentialsCache credentialsCache = await credentialsCacheTask;
                        IAuthenticator authenticator = await authenticatorTask;
                        IConnectionProvider connectionProvider = await connectionProviderTask;
                        ITransportSettings transportSettings = await transportSettingsTask;
                        var webSocketListenerRegistry = c.Resolve<IWebSocketListenerRegistry>();
                        AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(
                            this.iotHubHostName,
                            authenticator,
                            identityFactory,
                            linkHandlerProvider,
                            connectionProvider,
                            credentialsCache);

                        return new AmqpProtocolHead(
                            transportSettings,
                            amqpSettings,
                            transportListenerProvider,
                            webSocketListenerRegistry,
                            authenticator,
                            identityFactory);
                    })
                .As<Task<AmqpProtocolHead>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
