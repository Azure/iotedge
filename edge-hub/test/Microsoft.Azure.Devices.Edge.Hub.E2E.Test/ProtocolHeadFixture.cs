// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
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
                string certificateValue = await SecretsHelper.GetSecret("IotHubMqttHeadCert2");
                byte[] cert = Convert.FromBase64String(certificateValue);
                var certificate = new X509Certificate2(cert);

                string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");

                ProtocolHeadFixtureCache.X509Certificate = certificate;
                ProtocolHeadFixtureCache.EdgeDeviceConnectionString = edgeDeviceConnectionString;

                // TODO - After IoTHub supports MQTT, remove this and move to using MQTT for upstream connections
                await ConnectToIotHub(edgeDeviceConnectionString);

                IWebHostBuilder webHostBuilder = WebHost.CreateDefaultBuilder()
                    .UseKestrel(options => { options.Listen(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, 443, listenOptions => { listenOptions.UseHttps(certificate); }); });
                IWebHost webHost = webHostBuilder
                    .UseStartup<Startup>().Build();

                container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;

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
                var httpProtocolHead = new HttpProtocolHead(webHost);
                this.protocolHead = new EdgeHubProtocolHead(new List<IProtocolHead> { mqttProtocolHead, amqpProtocolHead, httpProtocolHead }, logger);
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
