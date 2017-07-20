// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
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

    public class ProtocolHeadFixture : IDisposable
    {
        IProtocolHead protocolHead;
        IContainer container;

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

        readonly IList<string> defaultRoutes = new List<string>() { "FROM /messages/events INTO $upstream" };
        const string DeviceId = "device1";

        public void Dispose()
        {
            if (this.protocolHead != null)
            {
                this.protocolHead.Dispose();
            }
        }

        public async Task<(IConnectionManager, Mock<IDeviceListener>)> StartMqttHeadWithMocks(IList<string> routes = null)
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

        public async Task StartMqttHead(IList<string> routes, Action<ContainerBuilder> setupMocks)
        {
            string certificateValue = await SecretsHelper.GetSecret("IotHubMqttHeadCert");
            byte[] cert = Convert.FromBase64String(certificateValue);
            var certificate = new X509Certificate2(cert);
            string iothubHostname = await SecretsHelper.GetSecret("IothubHostname");
            var topics = new MessageAddressConversionConfiguration(this.inboundTemplates, this.outboundTemplates);
            routes = routes ?? this.defaultRoutes;

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

            builder.RegisterModule(new CommonModule(iothubHostname, DeviceId));
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration.Object, topics));
            builder.RegisterModule(new RoutingModule(iothubHostname, DeviceId, routes));
            setupMocks?.Invoke(builder);
            this.container = builder.Build();

            IMqttConnectionProvider mqttConnectionProvider = await this.container.Resolve<Task<IMqttConnectionProvider>>();
            this.protocolHead = new MqttProtocolHead(container.Resolve<ISettingsProvider>(), certificate, mqttConnectionProvider, container.Resolve<IDeviceIdentityProvider>(), container.Resolve<ISessionStatePersistenceProvider>());

            await this.protocolHead.StartAsync();
        }
    }
}
