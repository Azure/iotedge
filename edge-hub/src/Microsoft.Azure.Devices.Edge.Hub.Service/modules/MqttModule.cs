// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class MqttModule : Module
    {
        readonly X509Certificate certificate;
        readonly MessageAddressConversionConfiguration conversionConfiguration;
        readonly string iothubHostname;
        readonly string deviceId;

        public MqttModule(X509Certificate certificate, MessageAddressConversionConfiguration conversionConfiguration, string iothubHostname, string deviceId)
        {
            this.certificate = Preconditions.CheckNotNull(certificate, nameof(certificate));
            this.conversionConfiguration = Preconditions.CheckNotNull(conversionConfiguration, nameof(conversionConfiguration));
            this.iothubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // MessageAddressConverter
            builder.Register(c => new MessageAddressConverter(this.conversionConfiguration))
                .As<MessageAddressConverter>()
                .SingleInstance();

            // IMessageConverter<IProtocolGatewayMessage>
            builder.Register(c => new ProtocolGatewayMessageConverter(c.Resolve<MessageAddressConverter>()))
                .As<IMessageConverter<IProtocolGatewayMessage>>()
                .SingleInstance();

            // ISettingsProvider
            builder.Register(c => new AppConfigSettingsProvider())
                .As<ISettingsProvider>()
                .SingleInstance();

            // Task<IMqttConnectionProvider>
            builder.Register(
                async c =>
                {
                    IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                    var connectionProvider = new ConnectionProvider(c.Resolve<IConnectionManager>(), edgeHub);

                    IMqttConnectionProvider mqtt = new MqttConnectionProvider(connectionProvider, c.Resolve<IMessageConverter<IProtocolGatewayMessage>>());
                    return mqtt;
                })
                .As<Task<IMqttConnectionProvider>>()
                .SingleInstance();

            // IAuthenticator
            builder.Register(c => new Authenticator(c.Resolve<IConnectionManager>(), this.deviceId))
                .As<IAuthenticator>()
                .SingleInstance();

            // IIdentityProvider
            builder.Register(
                c =>
                {
                    var identityFactory = new IdentityFactory(this.iothubHostname);
                    return new SasTokenDeviceIdentityProvider(c.Resolve<IAuthenticator>(), identityFactory);
                })
                .As<IDeviceIdentityProvider>()
                .SingleInstance();

            // ISessionStatePersistenceProvider
            builder.Register(c => new SessionStatePersistenceProvider(c.Resolve<IConnectionManager>()))
                .As<ISessionStatePersistenceProvider>()
                .SingleInstance();

            // IProtocolHead
            builder.Register(
                async c =>
                {
                    IMqttConnectionProvider connectionProvider = await c.Resolve<Task<IMqttConnectionProvider>>();
                    IProtocolHead head = new MqttProtocolHead(c.Resolve<ISettingsProvider>(), this.certificate, connectionProvider, c.Resolve<IDeviceIdentityProvider>(), c.Resolve<ISessionStatePersistenceProvider>());
                    return head;
                })
                .As<Task<IProtocolHead>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}