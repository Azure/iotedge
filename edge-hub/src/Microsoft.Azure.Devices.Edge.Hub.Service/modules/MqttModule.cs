// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Configuration;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class MqttModule : Module
    {
        readonly MessageAddressConversionConfiguration conversionConfiguration;

        readonly IConfiguration mqttSettingsConfiguration;

        // TODO: This causes reSharperWarning. Remove this TODO once code below are uncommented.
        // ReSharper disable once NotAccessedField.Local
        readonly bool isStoreAndForwardEnabled;
        readonly X509Certificate2 tlsCertificate;
        readonly bool clientCertAuthAllowed;
        readonly bool optimizeForPerformance;
        readonly SslProtocols sslProtocols;

        public MqttModule(
            IConfiguration mqttSettingsConfiguration,
            MessageAddressConversionConfiguration conversionConfiguration,
            X509Certificate2 tlsCertificate,
            bool isStoreAndForwardEnabled,
            bool clientCertAuthAllowed,
            bool optimizeForPerformance,
            SslProtocols sslProtocols)
        {
            this.mqttSettingsConfiguration = Preconditions.CheckNotNull(mqttSettingsConfiguration, nameof(mqttSettingsConfiguration));
            this.conversionConfiguration = Preconditions.CheckNotNull(conversionConfiguration, nameof(conversionConfiguration));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.optimizeForPerformance = optimizeForPerformance;
            this.sslProtocols = sslProtocols;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IByteBufferAllocator
            builder.Register(
                    c =>
                    {
                        // TODO - We should probably also use some heuristics to make this determination, like how much memory does the system have.
                        return this.optimizeForPerformance ? PooledByteBufferAllocator.Default : UnpooledByteBufferAllocator.Default as IByteBufferAllocator;
                    })
                .As<IByteBufferAllocator>()
                .SingleInstance();

            builder.Register(c => new ByteBufferConverter(c.Resolve<IByteBufferAllocator>()))
                .As<IByteBufferConverter>()
                .SingleInstance();

            // MessageAddressConverter
            builder.Register(c => new MessageAddressConverter(this.conversionConfiguration))
                .As<MessageAddressConverter>()
                .SingleInstance();

            // IMessageConverter<IProtocolGatewayMessage>
            builder.Register(c => new ProtocolGatewayMessageConverter(c.Resolve<MessageAddressConverter>(), c.Resolve<IByteBufferConverter>()))
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
                        var pgMessageConverter = c.Resolve<IMessageConverter<IProtocolGatewayMessage>>();
                        var byteBufferConverter = c.Resolve<IByteBufferConverter>();
                        IConnectionProvider connectionProvider = await c.Resolve<Task<IConnectionProvider>>();
                        IMqttConnectionProvider mqtt = new MqttConnectionProvider(connectionProvider, pgMessageConverter, byteBufferConverter);
                        return mqtt;
                    })
                .As<Task<IMqttConnectionProvider>>()
                .SingleInstance();

            // Task<ISessionStatePersistenceProvider>
            builder.Register(
                    async c =>
                    {
                        if (this.isStoreAndForwardEnabled)
                        {
                            IDbStoreProvider dbStoreProvider = await c.Resolve<Task<IDbStoreProvider>>();
                            IEntityStore<string, SessionState> entityStore = new StoreProvider(dbStoreProvider).GetEntityStore<string, SessionState>(Constants.SessionStorePartitionKey);
                            IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                            return new SessionStateStoragePersistenceProvider(edgeHub, entityStore) as ISessionStatePersistenceProvider;
                        }
                        else
                        {
                            IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                            return new SessionStatePersistenceProvider(edgeHub) as ISessionStatePersistenceProvider;
                        }
                    })
                .As<Task<ISessionStatePersistenceProvider>>()
                .SingleInstance();

            // MqttProtocolHead
            builder.Register(
                    async c =>
                    {
                        var productInfoStore = await c.Resolve<Task<IProductInfoStore>>();
                        var settingsProvider = c.Resolve<ISettingsProvider>();
                        var websocketListenerRegistry = c.Resolve<IWebSocketListenerRegistry>();
                        var byteBufferAllocator = c.Resolve<IByteBufferAllocator>();
                        var mqttConnectionProviderTask = c.Resolve<Task<IMqttConnectionProvider>>();
                        var sessionStatePersistenceProviderTask = c.Resolve<Task<ISessionStatePersistenceProvider>>();
                        var authenticatorProviderTask = c.Resolve<Task<IAuthenticator>>();
                        IClientCredentialsFactory clientCredentialsProvider = c.Resolve<IClientCredentialsFactory>();
                        IMqttConnectionProvider mqttConnectionProvider = await mqttConnectionProviderTask;
                        ISessionStatePersistenceProvider sessionStatePersistenceProvider = await sessionStatePersistenceProviderTask;
                        IAuthenticator authenticator = await authenticatorProviderTask;
                        return new MqttProtocolHead(
                            settingsProvider,
                            this.tlsCertificate,
                            mqttConnectionProvider,
                            authenticator,
                            clientCredentialsProvider,
                            sessionStatePersistenceProvider,
                            websocketListenerRegistry,
                            byteBufferAllocator,
                            productInfoStore,
                            this.clientCertAuthAllowed,
                            this.sslProtocols);
                    })
                .As<Task<MqttProtocolHead>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
