// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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
        readonly bool isStoreAndForwardEnabled;

        public MqttModule(IConfiguration mqttSettingsConfiguration, MessageAddressConversionConfiguration conversionConfiguration, bool isStoreAndForwardEnabled)
        {
            this.mqttSettingsConfiguration = Preconditions.CheckNotNull(mqttSettingsConfiguration, nameof(mqttSettingsConfiguration));
            this.conversionConfiguration = Preconditions.CheckNotNull(conversionConfiguration, nameof(conversionConfiguration));
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
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
                    IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                    var connectionProvider = new ConnectionProvider(c.Resolve<IConnectionManager>(), edgeHub);

                    IMqttConnectionProvider mqtt = new MqttConnectionProvider(connectionProvider, c.Resolve<IMessageConverter<IProtocolGatewayMessage>>());
                    return mqtt;
                })
                .As<Task<IMqttConnectionProvider>>()
                .SingleInstance();

            // IIdentityProvider
            builder.Register(
                c =>
                {
                    return new SasTokenDeviceIdentityProvider(c.Resolve<IAuthenticator>(), c.Resolve<IIdentityFactory>());
                })
                .As<IDeviceIdentityProvider>()
                .SingleInstance();

            // ISessionStatePersistenceProvider
            builder.Register<ISessionStatePersistenceProvider>(
                    c =>
                    {
                        if (this.isStoreAndForwardEnabled)
                        {
                            IEntityStore<string, ISessionState> entityStore = new StoreProvider(c.Resolve<IDbStoreProvider>()).GetEntityStore<string, ISessionState>(Core.Constants.SessionStorePartitionKey);
                            return new SessionStateStoragePersistenceProvider(c.Resolve<IConnectionManager>(), entityStore);
                        }
                        else
                        {
                            return new SessionStatePersistenceProvider(c.Resolve<IConnectionManager>());
                        }

                    })
                .As<ISessionStatePersistenceProvider>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}