// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
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

        public AmqpModule(
            string scheme,
            int port,
            X509Certificate2 tlsCertificate,
            string iotHubHostName)
        {
            this.scheme = Preconditions.CheckNonWhiteSpace(scheme, nameof(scheme));
            this.port = Preconditions.CheckRange(port, 0, ushort.MaxValue, nameof(port));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ITransportSettings
            builder.Register(c => new DefaultTransportSettings(this.scheme, HostName, this.port, this.tlsCertificate))
                .As<ITransportSettings>()
                .SingleInstance();

            // ITransportListenerProvider
            builder.Register(c => new AmqpTransportListenerProvider())
                .As<ITransportListenerProvider>()
                .SingleInstance();

            // ILinkHandlerProvider
            builder.Register(
                c =>
                {
                    IMessageConverter<AmqpMessage> messageConverter = new AmqpMessageConverter();
                    IMessageConverter<AmqpMessage> twinMessageConverter = new AmqpTwinMessageConverter();
                    IMessageConverter<AmqpMessage> directMethodMessageConverter = new AmqpDirectMethodMessageConverter();
                    ILinkHandlerProvider linkHandlerProvider = new LinkHandlerProvider(messageConverter, twinMessageConverter, directMethodMessageConverter);
                    return linkHandlerProvider;
                })
                .As<ILinkHandlerProvider>()
                .SingleInstance();

            // Task<AmqpProtocolHead>
            builder.Register(
                async c =>
                {
                    var credentialsStore = c.Resolve<ICredentialsStore>();
                    var authenticator = c.Resolve<IAuthenticator>();
                    var identityFactory = c.Resolve<IClientCredentialsFactory>();
                    var transportSettings = c.Resolve<ITransportSettings>();
                    var transportListenerProvider = c.Resolve<ITransportListenerProvider>();
                    var linkHandlerProvider = c.Resolve<ILinkHandlerProvider>();
                    IConnectionProvider connectionProvider = await c.Resolve<Task<IConnectionProvider>>();
                    AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(
                        this.iotHubHostName,
                        authenticator,
                        identityFactory,
                        linkHandlerProvider,
                        connectionProvider,
                        credentialsStore);
                    return new AmqpProtocolHead(
                        transportSettings,
                        amqpSettings,
                        transportListenerProvider);
                })
                .As<Task<AmqpProtocolHead>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
