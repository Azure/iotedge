// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Moq;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class ProtocolHeadFixture
    {
        IProtocolHead protocolHead;
        IContainer container;
        static readonly ProtocolHeadFixture instance = new ProtocolHeadFixture();

        readonly IList<string> inboundTemplates = new List<string>()
        {
            "devices/{deviceId}/messages/events/{params}/",
            "devices/{deviceId}/messages/events/",
            "devices/{deviceId}/modules/{moduleId}/messages/events/{params}/",
            "devices/{deviceId}/modules/{moduleId}/messages/events/",
            "$iothub/methods/res/{statusCode}/?$rid={correlationId}",
            "$iothub/methods/res/{statusCode}/?$rid={correlationId}&foo={bar}"
        };

        readonly IDictionary<string, string> outboundTemplates = new Dictionary<string, string>()
        {
            { "C2D", "devices/{deviceId}/messages/devicebound"},
            { "TwinEndpoint", "$iothub/twin/res/{statusCode}/?$rid={correlationId}"},
            { "TwinDesiredPropertyUpdate", "$iothub/twin/PATCH/properties/desired/?$version={version}"},
            { "ModuleEndpoint", "devices/{deviceId}/modules/{moduleId}/inputs/{inputName}"}
        };

        readonly IDictionary<string, string> routes = new Dictionary<string, string>() {
            ["r1"] = "FROM /messages/events INTO $upstream",
            ["r2"] = "FROM /messages/modules/senderA INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            ["r3"] = "FROM /messages/modules/senderB INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            ["r4"] = "FROM /messages/modules/sender1 INTO BrokeredEndpoint(\"/modules/receiver1/inputs/input1\")",
            ["r5"] = "FROM /messages/modules/sender2 INTO BrokeredEndpoint(\"/modules/receiver2/inputs/input1\")",
            ["r6"] = "FROM /messages/modules/sender3 INTO BrokeredEndpoint(\"/modules/receiver3/inputs/input1\")",
            ["r7"] = "FROM /messages/modules/sender4 INTO BrokeredEndpoint(\"/modules/receiver4/inputs/input1\")",
            ["r8"] = "FROM /messages/modules/sender5 INTO BrokeredEndpoint(\"/modules/receiver5/inputs/input1\")",
            ["r9"] = "FROM /messages/modules/sender6 INTO BrokeredEndpoint(\"/modules/receiver6/inputs/input1\")",
            ["r10"] = "FROM /messages/modules/sender7 INTO BrokeredEndpoint(\"/modules/receiver7/inputs/input1\")",
            ["r11"] = "FROM /messages/modules/sender8 INTO BrokeredEndpoint(\"/modules/receiver8/inputs/input1\")",
            ["r12"] = "FROM /messages/modules/sender9 INTO BrokeredEndpoint(\"/modules/receiver9/inputs/input1\")",
            ["r13"] = "FROM /messages/modules/sender10 INTO BrokeredEndpoint(\"/modules/receiver10/inputs/input1\")"
        };

        public static ProtocolHeadFixture GetInstance()
        {
            return instance;
        }

        private ProtocolHeadFixture()
        {
            bool.TryParse(ConfigHelper.TestConfig["Tests_StartEdgeHubService"], out bool shouldStartEdge);
            if (shouldStartEdge)
            {
                this.StartMqttHead(this.routes, null).Wait();
            }
        }

        ~ProtocolHeadFixture()
        {
            if (this.protocolHead != null)
            {
                this.protocolHead.Dispose();
            }
        }

        public async Task<(IConnectionManager, Mock<IDeviceListener>)> StartMqttHeadWithMocks(IDictionary<string, string> routes = null)
        {
            var deviceListener = new Mock<IDeviceListener>();
            IConnectionManager connectionManager = null;

            Action<ContainerBuilder> mockSetup = (builder) =>
            {
                // Register ISessionStatePersistenceProvider to capture connectionManager
                builder.Register(
                        c =>
                        {
                            connectionManager = c.Resolve<IConnectionManager>();
                            var sesssionPersistenceProvider = new SessionStatePersistenceProvider(connectionManager);
                            return sesssionPersistenceProvider;
                        })
                    .As<ISessionStatePersistenceProvider>()
                    .SingleInstance();

                // Register IMqttConnectionProvider here to mock it and to be able to mock DeviceIdentity
                builder.Register(
                        async c =>
                        {
                            IEdgeHub edgeHub = await c.Resolve<Task<IEdgeHub>>();
                            var connectionProvider = new ConnectionProvider(c.Resolve<IConnectionManager>(), edgeHub);

                            var mqtt = new Mock<IMqttConnectionProvider>();
                            IMessagingServiceClient messagingServiceClient = new MessagingServiceClient(deviceListener.Object, c.Resolve<IMessageConverter<IProtocolGatewayMessage>>());
                            IDeviceIdentity deviceidentity = null;
                            mqtt.Setup(
                                p => p.Connect(It.IsAny<IDeviceIdentity>())).Callback<IDeviceIdentity>(
                                id =>
                                {
                                    deviceidentity = id;
                                    var identity = (deviceidentity as ProtocolGatewayIdentity).Identity;
                                    deviceListener.Setup(p => p.Identity).Returns(identity);
                                    deviceListener.Setup(p => p.BindDeviceProxy(It.IsAny<IDeviceProxy>())).Callback<IDeviceProxy>(
                                        async deviceProxy =>
                                        {
                                            Try<ICloudProxy> cloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(identity);
                                            ICloudListener cloudListener = new CloudListener(deviceProxy, edgeHub, identity);
                                            cloudProxy.Value.BindCloudListener(cloudListener);
                                            connectionManager.AddDeviceConnection(identity, deviceProxy);
                                        });
                                    deviceListener.Setup(p => p.CloseAsync()).Callback(
                                        () =>
                                        {
                                            connectionManager.RemoveDeviceConnection(deviceidentity.Id);
                                        }).Returns(TaskEx.Done);
                                }).Returns(Task.FromResult((IMessagingBridge)new SingleClientMessagingBridge(deviceidentity, messagingServiceClient)));

                            return mqtt.Object;
                        })
                    .As<Task<IMqttConnectionProvider>>()
                    .SingleInstance();
            };
            await this.StartMqttHead(routes, mockSetup);
            return (connectionManager, deviceListener);
        }

        public async Task StartMqttHead(IDictionary<string, string> routes, Action<ContainerBuilder> setupMocks)
        {
            const int ConnectionPoolSize = 10;
            string certificateValue = await SecretsHelper.GetSecret("IotHubMqttHeadCert");
            byte[] cert = Convert.FromBase64String(certificateValue);
            var certificate = new X509Certificate2(cert);
            string edgeHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableHubConnStrKey");
            Client.IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Client.IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
            var topics = new MessageAddressConversionConfiguration(this.inboundTemplates, this.outboundTemplates);

            var builder = new ContainerBuilder();
            builder.RegisterModule(new LoggingModule());

            var mqttSettingsConfiguration = new Mock<IConfiguration>();
            mqttSettingsConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>(s => s.Value == null));

            builder.RegisterBuildCallback(
                c =>
                {
                    // set up loggers for dotnetty
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    InternalLoggerFactory.DefaultFactory = loggerFactory;

                    var eventListener = new LoggerEventListener(loggerFactory.CreateLogger("ProtocolGateway"));
                    eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
                });

            var storeAndForwardConfiguration = new StoreAndForwardConfiguration(-1);
            builder.RegisterModule(new CommonModule(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId));
            builder.RegisterModule(new RoutingModule(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, edgeHubConnectionString, routes, false, storeAndForwardConfiguration, ConnectionPoolSize, false));
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration.Object, topics, false));
            setupMocks?.Invoke(builder);
            this.container = builder.Build();

            IConfigSource configSource = await this.container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await this.container.Resolve<Task<ConfigUpdater>>();
            await configUpdater.Init(configSource);

            IMqttConnectionProvider mqttConnectionProvider = await this.container.Resolve<Task<IMqttConnectionProvider>>();
            this.protocolHead = new MqttProtocolHead(container.Resolve<ISettingsProvider>(), certificate, mqttConnectionProvider, container.Resolve<IDeviceIdentityProvider>(), container.Resolve<ISessionStatePersistenceProvider>());

            await this.protocolHead.StartAsync();
        }
    }
}
