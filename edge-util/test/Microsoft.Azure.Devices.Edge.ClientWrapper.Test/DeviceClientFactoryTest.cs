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

        const string EdgehubConnectionstringVariableName = "EdgeHubConnectionString";
        const string EdgeletUriVariableName = "IOTEDGE_IOTEDGEDURI";
        const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";
        const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        const string ModuleIdVariableName = "IOTEDGE_MODULEID";
        const string AuthSchemeVariableName = "IOTEDGE_AUTHSCHEME";

        public DeviceClientFactoryTest(EdgeletFixture fixture)
        {
            this.serverUrl = fixture.ServiceUrl;
            this.iotHubConnectionString = "Hostname=iothub.test;DeviceId=device1;ModuleId=module1;SharedAccessKey=" + Convert.ToBase64String(this.sasKey);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgehubConnectionstringVariableName, this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory().Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable(EdgehubConnectionstringVariableName, null);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_SetTransportType_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgehubConnectionstringVariableName, this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory(TransportType.Mqtt_Tcp_Only).Create();

            Assert.NotNull(dc);
        }

        [Fact]
        public void TestCreate_FromConnectionStringEnvironment_SetTransportSettings_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgehubConnectionstringVariableName, this.iotHubConnectionString);
            DeviceClient dc = new DeviceClientFactory(new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) } ).Create();

            Assert.NotNull(dc);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_MissingVariable_ShouldThrow()
        {
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, this.serverUrl);
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_UnsupportedAuth_ShouldThrow()
        {
            Environment.SetEnvironmentVariable(EdgeletUriVariableName, this.serverUrl);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");

            Environment.SetEnvironmentVariable(AuthSchemeVariableName, "x509Cert");
            Assert.Throws<InvalidOperationException>(() => new DeviceClientFactory().Create());

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgeletUriVariableName, this.serverUrl);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, "sasToken");

            DeviceClient dc = new DeviceClientFactory().Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_SetTransportType_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgeletUriVariableName, this.serverUrl);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, "sasToken");

            DeviceClient dc = new DeviceClientFactory(TransportType.Mqtt_Tcp_Only).Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, null);
        }

        [Fact]
        public void TestCreate_FromEdgeletEnvironment_SetTransportSettings_ShouldCreateClient()
        {
            Environment.SetEnvironmentVariable(EdgeletUriVariableName, this.serverUrl);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, "sasToken");

            DeviceClient dc = new DeviceClientFactory(new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) }).Create();

            Assert.NotNull(dc);

            Environment.SetEnvironmentVariable(EdgeletUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, null);
        }
    }
}
