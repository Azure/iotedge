// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DeviceClientFactoryTest : IClassFixture<EdgeletFixture>
    {
        readonly string serverUrl;
        readonly byte[] sasKey = System.Text.Encoding.UTF8.GetBytes("key");
        readonly string iotHubConnectionString;

        public DeviceClientFactoryTest(EdgeletFixture fixture)
        {
            this.serverUrl = fixture.ServiceUrl;
            this.iotHubConnectionString = "Hostname=iothub.test;DeviceId=device1;ModuleId=module1;SharedAccessKey=" + Convert.ToBase64String(this.sasKey);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("EdgeHubConnectionString", this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory().Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable("EdgeHubConnectionString", null);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_SetTransportType_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("EdgeHubConnectionString", this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory(TransportType.Mqtt_Tcp_Only).Create();

            Assert.NotNull(dc);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_SetTransportSettings_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("EdgeHubConnectionString", this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory(new ITransportSettings[1] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) } ).Create();

            Assert.NotNull(dc);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_MissingVariable_ShouldThrow()
        {
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", this.serverUrl);
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", "iothub.test");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_Gateway", "localhost");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_DeviceId", "device1");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_ModuleId", "module1");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", null);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", null);
            Environment.SetEnvironmentVariable("IotEdge_Gateway", null);
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", null);
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_UnsupportedAuth_ShouldThrow()
        {
            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", this.serverUrl);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", "iothub.test");
            Environment.SetEnvironmentVariable("IotEdge_Gateway", "localhost");
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", "device1");
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", "module1");

            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", "x509Cert");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", null);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", null);
            Environment.SetEnvironmentVariable("IotEdge_Gateway", null);
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", null);
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", this.serverUrl);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", "iothub.test");
            Environment.SetEnvironmentVariable("IotEdge_Gateway", "localhost");
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", "device1");
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", "module1");
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", "sasToken");

            DeviceClient dc = new DeviceClientFactory().Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", null);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", null);
            Environment.SetEnvironmentVariable("IotEdge_Gateway", null);
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", null);
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", null);
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_SetTransportType_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", this.serverUrl);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", "iothub.test");
            Environment.SetEnvironmentVariable("IotEdge_Gateway", "localhost");
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", "device1");
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", "module1");
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", "sasToken");

            DeviceClient dc = new DeviceClientFactory(TransportType.Mqtt_Tcp_Only).Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", null);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", null);
            Environment.SetEnvironmentVariable("IotEdge_Gateway", null);
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", null);
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", null);
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_SetTransportSettings_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", this.serverUrl);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", "iothub.test");
            Environment.SetEnvironmentVariable("IotEdge_Gateway", "localhost");
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", "device1");
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", "module1");
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", "sasToken");

            DeviceClient dc = new DeviceClientFactory(new ITransportSettings[1] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) }).Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable("IotEdge_EdgeletUri", null);
            Environment.SetEnvironmentVariable("IotEdge_IotHubHostname", null);
            Environment.SetEnvironmentVariable("IotEdge_Gateway", null);
            Environment.SetEnvironmentVariable("IotEdge_DeviceId", null);
            Environment.SetEnvironmentVariable("IotEdge_ModuleId", null);
            Environment.SetEnvironmentVariable("IotEdge_AuthScheme", null);
        }
    }
}
