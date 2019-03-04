// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Service.Constants;

    public class ProtocolHeadFixture : IDisposable
    {
        public ProtocolHeadFixture()
        {
            this.ProtocolHead = InternalProtocolHeadFixture.Instance.ProtocolHead;
        }

        public IProtocolHead ProtocolHead { get; }

        public void Dispose()
        {
        }

        public class InternalProtocolHeadFixture
        {
            IContainer container;
            IProtocolHead protocolHead;

            InternalProtocolHeadFixture()
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

            public static InternalProtocolHeadFixture Instance { get; } = new InternalProtocolHeadFixture();

            public IProtocolHead ProtocolHead => this.protocolHead;

            // Device SDK caches the AmqpTransportSettings that are set the first time and ignores
            // all the settings used thereafter from that process. So set up a dummy connection using the test
            // AmqpTransportSettings, so that Device SDK caches it and uses it thereafter
            static async Task ConnectToIotHub(string connectionString)
            {
                DeviceClient dc = DeviceClient.CreateFromConnectionString(connectionString, TestSettings.AmqpTransportSettings);
                await dc.OpenAsync();
                await dc.CloseAsync();
            }

            async Task StartProtocolHead()
            {
                string certificateValue = await SecretsHelper.GetSecret("IotHubMqttHeadCert");
                byte[] cert = Convert.FromBase64String(certificateValue);
                var certificate = new X509Certificate2(cert);
                // TODO for now this is empty as will suffice for SAS X.509 thumbprint auth but we will need other CA certs for X.509 CA validation
                var trustBundle = new List<X509Certificate2>();

                string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");

                // TODO - After IoTHub supports MQTT, remove this and move to using MQTT for upstream connections
                await ConnectToIotHub(edgeDeviceConnectionString);

                ConfigHelper.TestConfig[Constants.ConfigKey.IotHubConnectionString] = edgeDeviceConnectionString;
                Hosting hosting = Hosting.Initialize(ConfigHelper.TestConfig, certificate, new DependencyManager(ConfigHelper.TestConfig, certificate, trustBundle), true);
                this.container = hosting.Container;

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
                var httpProtocolHead = new HttpProtocolHead(hosting.WebHost);
                this.protocolHead = new EdgeHubProtocolHead(new List<IProtocolHead> { mqttProtocolHead, amqpProtocolHead, httpProtocolHead }, logger);
                await this.protocolHead.StartAsync();
            }
        }
    }
}
