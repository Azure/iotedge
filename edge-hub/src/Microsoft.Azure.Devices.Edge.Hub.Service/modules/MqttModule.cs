// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Configuration;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class MqttModule : Module
    {
        readonly MessageAddressConversionConfiguration conversionConfiguration;
        readonly IConfiguration mqttSettingsConfiguration;
        //TODO: This causes reSharperWarning. Remove this TODO once code below are uncommented. 
        // ReSharper disable once NotAccessedField.Local
        readonly bool isStoreAndForwardEnabled;
        readonly X509Certificate2 tlsCertificate;
        readonly bool clientCertAuthAllowed;
        readonly string caChainPath;

        public MqttModule(
            IConfiguration mqttSettingsConfiguration,
            MessageAddressConversionConfiguration conversionConfiguration,
            X509Certificate2 tlsCertificate,
            bool isStoreAndForwardEnabled,
            bool clientCertAuthAllowed,
            string caChainPath)
        {
            this.mqttSettingsConfiguration = Preconditions.CheckNotNull(mqttSettingsConfiguration, nameof(mqttSettingsConfiguration));
            this.conversionConfiguration = Preconditions.CheckNotNull(conversionConfiguration, nameof(conversionConfiguration));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.caChainPath = caChainPath;
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
            builder.Register(c => new MqttSettingsProvider(this.mqttSettingsConfiguration))
                .As<ISettingsProvider>()
                .SingleInstance();

            // Task<IMqttConnectionProvider>
            builder.Register(
                async c =>
                {
                    IConnectionProvider connectionProvider = await c.Resolve<Task<IConnectionProvider>>();
                    IMqttConnectionProvider mqtt = new MqttConnectionProvider(connectionProvider, c.Resolve<IMessageConverter<IProtocolGatewayMessage>>());
                    return mqtt;
                })
                .As<Task<IMqttConnectionProvider>>()
                .SingleInstance();

            // IIdentityProvider
            builder.Register(c => new DeviceIdentityProvider(c.Resolve<IAuthenticator>(), c.Resolve<IClientCredentialsFactory>(), this.clientCertAuthAllowed) as IDeviceIdentityProvider)
                .As<IDeviceIdentityProvider>()
                .SingleInstance();

            // ISessionStatePersistenceProvider
            builder.Register<ISessionStatePersistenceProvider>(
                    c =>
                    {
                        if (this.isStoreAndForwardEnabled)
                        {
                            IEntityStore<string, SessionState> entityStore = new StoreProvider(c.Resolve<IDbStoreProvider>()).GetEntityStore<string, SessionState>(Core.Constants.SessionStorePartitionKey);
                            return new SessionStateStoragePersistenceProvider(c.Resolve<IConnectionManager>(), entityStore);
                        }
                        else
                        {
                            return new SessionStatePersistenceProvider(c.Resolve<IConnectionManager>());
                        }
                    })
                .As<ISessionStatePersistenceProvider>()
                .SingleInstance();

            // MqttProtocolHead
            builder.Register(
                    async c => new MqttProtocolHead(
                        c.Resolve<ISettingsProvider>(),
                        this.tlsCertificate,
                        await c.Resolve<Task<IMqttConnectionProvider>>(),
                        c.Resolve<IDeviceIdentityProvider>(),
                        c.Resolve<ISessionStatePersistenceProvider>(),
                        c.Resolve<IWebSocketListenerRegistry>(),
                        this.clientCertAuthAllowed,
                        this.caChainPath
                    ))
                .As<Task<MqttProtocolHead>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
