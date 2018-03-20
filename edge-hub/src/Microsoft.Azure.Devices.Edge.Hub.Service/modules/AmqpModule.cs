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
        readonly string scheme;
        readonly string hostName;
        readonly int port;
        readonly X509Certificate2 tlsCertificate;
        readonly string iotHubHostName;

        public AmqpModule(
            string scheme,
            string hostName,
            int port,
            X509Certificate2 tlsCertificate,
            string iotHubHostName)
        {
            this.scheme = Preconditions.CheckNonWhiteSpace(scheme, nameof(scheme));
            this.hostName = Preconditions.CheckNonWhiteSpace(hostName, nameof(hostName));
            this.port = Preconditions.CheckRange(port, 0, ushort.MaxValue, nameof(port));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ITransportSettings
            builder.Register(c => new DefaultTransportSettings(this.scheme, this.hostName, this.port, this.tlsCertificate))
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
                    var authenticator = c.Resolve<IAuthenticator>();
                    var identityFactory = c.Resolve<IIdentityFactory>();
                    var transportSettings = c.Resolve<ITransportSettings>();
                    var transportListenerProvider = c.Resolve<ITransportListenerProvider>();
                    var linkHandlerProvider = c.Resolve<ILinkHandlerProvider>();
                    IConnectionProvider connectionProvider = await c.Resolve<Task<IConnectionProvider>>();
                    AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(
                        this.hostName,
                        this.iotHubHostName,
                        this.tlsCertificate,
                        authenticator,
                        identityFactory,
                        linkHandlerProvider,
                        connectionProvider);
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
