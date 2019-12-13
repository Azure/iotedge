// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
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
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using EdgeHubConstants = Microsoft.Azure.Devices.Edge.Hub.Service.Constants;

    public class ProtocolHeadFixture : IDisposable
    {
        const SslProtocols DefaultSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        readonly SslProtocols sslProtocols;
        IProtocolHead protocolHead;
        Hosting hosting;
        bool disposed;

        public ProtocolHeadFixture()
            : this(DefaultSslProtocols)
        {
        }

        public ProtocolHeadFixture(SslProtocols? sslProtocols)
        {
            this.sslProtocols = sslProtocols ?? DefaultSslProtocols;
            bool.TryParse(ConfigHelper.TestConfig["Tests_StartEdgeHubService"], out bool shouldStartEdge);
            if (shouldStartEdge)
            {
                this.StartProtocolHead().Wait();
            }

            this.IsClosed = false;
        }

        ~ProtocolHeadFixture()
        {
            this.Dispose(false);
        }

        public bool IsClosed { get; private set; }

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

            ConfigHelper.TestConfig[EdgeHubConstants.ConfigKey.IotHubConnectionString] = edgeDeviceConnectionString;

            IDependencyManager dependencyManager = new DependencyManager(ConfigHelper.TestConfig, certificate, trustBundle, this.sslProtocols);
            Hosting hosting = Hosting.Initialize(ConfigHelper.TestConfig, certificate, dependencyManager, true, this.sslProtocols);
            this.hosting = hosting;
            IContainer container = hosting.Container;

            // CloudConnectionProvider and RoutingEdgeHub have a circular dependency. So set the
            // EdgeHub on the CloudConnectionProvider before any other operation
            ICloudConnectionProvider cloudConnectionProvider = await container.Resolve<Task<ICloudConnectionProvider>>();
            IEdgeHub edgeHub = await container.Resolve<Task<IEdgeHub>>();
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            IConfigSource configSource = await container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await container.Resolve<Task<ConfigUpdater>>();
            await configUpdater.Init(configSource);

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            MqttProtocolHead mqttProtocolHead = await container.Resolve<Task<MqttProtocolHead>>();
            AmqpProtocolHead amqpProtocolHead = await container.Resolve<Task<AmqpProtocolHead>>();
            var httpProtocolHead = new HttpProtocolHead(hosting.WebHost);
            this.protocolHead = new EdgeHubProtocolHead(new List<IProtocolHead> { mqttProtocolHead, amqpProtocolHead, httpProtocolHead }, logger);
            await this.protocolHead.StartAsync();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Close the fixture before disposing to avoid cases where it hasn't been closed by the test class.
                this.CloseAsync().Wait();

                this.protocolHead?.Dispose();
                this.hosting?.Container?.Dispose();
            }

            this.disposed = true;
        }

        public async Task CloseAsync()
        {
            await this.protocolHead?.CloseAsync(CancellationToken.None);

            if (this.hosting != null)
            {
                IContainer container = this.hosting.Container;
                IDbStoreProvider dbStoreProvider = await container.Resolve<Task<IDbStoreProvider>>();
                await dbStoreProvider.CloseAsync();
            }

            this.IsClosed = true;
        }
    }
}
