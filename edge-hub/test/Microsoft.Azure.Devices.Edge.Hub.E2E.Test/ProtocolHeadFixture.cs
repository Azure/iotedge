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
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class ProtocolHeadFixture : IDisposable
    {
        public IProtocolHead ProtocolHead { get; }

        public ProtocolHeadFixture()
        {
            this.ProtocolHead = InternalProtocolHeadFixture.Instance.ProtocolHead;
        }

        public void Dispose()
        {
        }

        public class InternalProtocolHeadFixture
        {
            IContainer container;
            IProtocolHead protocolHead;

            public IProtocolHead ProtocolHead => this.protocolHead;

            public static InternalProtocolHeadFixture Instance { get; } = new InternalProtocolHeadFixture();

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
                { "C2D", "devices/{deviceId}/messages/devicebound" },
                { "TwinEndpoint", "$iothub/twin/res/{statusCode}/?$rid={correlationId}" },
                { "TwinDesiredPropertyUpdate", "$iothub/twin/PATCH/properties/desired/?$version={version}" },
                { "ModuleEndpoint", "devices/{deviceId}/modules/{moduleId}/inputs/{inputName}" }
            };

            readonly IDictionary<string, string> routes = new Dictionary<string, string>()
            {
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
                ["r13"] = "FROM /messages/modules/sender10 INTO BrokeredEndpoint(\"/modules/receiver10/inputs/input1\")",
                ["r14"] = "FROM /messages/modules/sender11/outputs/output1 INTO BrokeredEndpoint(\"/modules/receiver11/inputs/input1\")",
                ["r15"] = "FROM /messages/modules/sender11/outputs/output2 INTO BrokeredEndpoint(\"/modules/receiver11/inputs/input2\")",
            };

            private InternalProtocolHeadFixture()
            {
                bool.TryParse(ConfigHelper.TestConfig["Tests_StartEdgeHubService"], out bool shouldStartEdge);
                if (shouldStartEdge)
                {
                    this.StartProtocolHead().Wait();
                }
            }

            ~InternalProtocolHeadFixture()
            {
                this.protocolHead?.Dispose();
            }

            async Task StartProtocolHead()
            {
                const int ConnectionPoolSize = 10;
                string certificateValue = await SecretsHelper.GetSecret("IotHubMqttHeadCert");
                byte[] cert = Convert.FromBase64String(certificateValue);
                var certificate = new X509Certificate2(cert);

                string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");

                // TODO - After IoTHub supports MQTT, remove this and move to using MQTT for upstream connections
                await ConnectToIotHub(edgeDeviceConnectionString);

                string edgeHubConnectionString = $"{edgeDeviceConnectionString};ModuleId=$edgeHub";
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

                var versionInfo = new VersionInfo("v1", "b1", "c1");
                var storeAndForwardConfiguration = new StoreAndForwardConfiguration(-1);
                builder.RegisterModule(
                    new CommonModule(
                        string.Empty,
                        iotHubConnectionStringBuilder.HostName,
                        iotHubConnectionStringBuilder.DeviceId,
                        iotHubConnectionStringBuilder.ModuleId,
                        string.Empty,
                        Option.None<string>(),
                        AuthenticationMode.CloudAndScope,
                        Option.Some(edgeDeviceConnectionString),
                        false,
                        false,
                        string.Empty,
                        Option.None<string>(),
                        TimeSpan.FromHours(1),
                        false));

                builder.RegisterModule(
                    new RoutingModule(
                        iotHubConnectionStringBuilder.HostName,
                        iotHubConnectionStringBuilder.DeviceId,
                        iotHubConnectionStringBuilder.ModuleId,
                        Option.Some(edgeHubConnectionString),
                        this.routes,
                        false,
                        storeAndForwardConfiguration,
                        ConnectionPoolSize,
                        false,
                        versionInfo,
                        Option.Some(UpstreamProtocol.Amqp),
                        TimeSpan.FromSeconds(5),
                        101,
                        TimeSpan.FromHours(8760)));

                builder.RegisterModule(new HttpModule());
                builder.RegisterModule(new MqttModule(mqttSettingsConfiguration.Object, topics, certificate, false, false, string.Empty, false));
                builder.RegisterModule(new AmqpModule("amqps", 5671, certificate, iotHubConnectionStringBuilder.HostName));
                this.container = builder.Build();

                // CloudConnectionProvider and RoutingEdgeHub have a circular dependency. So set the
                // EdgeHub on the CloudConnectionProvider before any other operation
                ICloudConnectionProvider cloudConnectionProvider = await this.container.Resolve<Task<ICloudConnectionProvider>>();
                IEdgeHub edgeHub = await this.container.Resolve<Task<IEdgeHub>>();
                cloudConnectionProvider.BindEdgeHub(edgeHub);

                IConfigSource configSource = await this.container.Resolve<Task<IConfigSource>>();
                ConfigUpdater configUpdater = await this.container.Resolve<Task<ConfigUpdater>>();
                await configUpdater.Init(configSource);

                ILogger logger = this.container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
                MqttProtocolHead mqttProtocolHead = await this.container.Resolve<Task<MqttProtocolHead>>();
                AmqpProtocolHead amqpProtocolHead = await this.container.Resolve<Task<AmqpProtocolHead>>();
                this.protocolHead = new EdgeHubProtocolHead(new List<IProtocolHead> { mqttProtocolHead, amqpProtocolHead }, logger);
                await this.protocolHead.StartAsync();
            }

            // Device SDK caches the AmqpTransportSettings that are set the first time and ignores
            // all the settings used thereafter from that process. So set up a dummy connection using the test
            // AmqpTransportSettings, so that Device SDK caches it and uses it thereafter
            static async Task ConnectToIotHub(string connectionString)
            {
                DeviceClient dc = DeviceClient.CreateFromConnectionString(connectionString, TestSettings.AmqpTransportSettings);
                await dc.OpenAsync();
                await dc.CloseAsync();
            }
        }
    }
}
