// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer;
    using Xunit;

    [Unit]
    public class ClientProviderTest : IClassFixture<WorkloadFixture>
    {
        const string IotHubHostName = "iothub.test";
        const string DeviceId = "device1";
        const string ModuleId = "module1";
        const string IotEdgedUriVariableName = "IOTEDGE_WORKLOADURI";
        const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";
        const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        const string ModuleIdVariableName = "IOTEDGE_MODULEID";
        const string AuthSchemeVariableName = "IOTEDGE_AUTHSCHEME";
        const string ModuleGeneratioIdVariableName = "IOTEDGE_MODULEGENERATIONID";

        readonly string authKey = Convert.ToBase64String("key".ToBytes());
        readonly Uri serverUrl;

        public ClientProviderTest(WorkloadFixture workloadFixture)
        {
            this.serverUrl = new Uri(workloadFixture.ServiceUrl);
        }

        [Fact]
        public void Test_Create_DeviceIdentity_WithAuthMethod_ShouldCreateDeviceClient()
        {
            string token = TokenHelper.CreateSasToken(IotHubHostName);
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);
            var authenticationMethod = new DeviceAuthenticationWithToken(DeviceId, token);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, authenticationMethod, options, Option.None<string>());
            Assert.NotNull(client);
            Assert.True(client is DeviceClientWrapper);
        }

        [Fact]
        public void Test_Create_DeviceIdentity_WithConnectionString_ShouldCreateDeviceClient()
        {
            string connectionString = $"HostName={IotHubHostName};DeviceId=device1;SharedAccessKey={this.authKey}";
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, connectionString, options);

            Assert.NotNull(client);
            Assert.True(client is DeviceClientWrapper);
        }

        [Fact]
        public async Task Test_Create_DeviceIdentity_WithEnv_ShouldThrow()
        {
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientProvider(Option.None<string>()).CreateAsync(identity, options));
        }

        [Fact]
        public void Test_Create_ModuleIdentity_WithAuthMethod_ShouldCreateModuleClient()
        {
            string token = TokenHelper.CreateSasToken(IotHubHostName);
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);
            var authenticationMethod = new ModuleAuthenticationWithToken(DeviceId, ModuleId, token);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, authenticationMethod, options, Option.None<string>());

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);
        }

        [Fact]
        public void Test_Create_ModuleIdentity_WithConnectionString_ShouldCreateModuleClient()
        {
            string connectionString = $"HostName={IotHubHostName};DeviceId={DeviceId};ModuleId={ModuleId};SharedAccessKey={this.authKey}";
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, connectionString, options);

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);
        }

        [Fact]
        public void Test_Create_ModuleIdentity_WithTokenProvider_ShouldCreateModuleClient()
        {
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.None<string>());

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);
        }

        [Fact]
        public void Test_Create_DeviceIdentity_WithTokenProvider_ShouldCreateDeviceClient()
        {
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.None<string>());

            Assert.NotNull(client);
            Assert.True(client is DeviceClientWrapper);
        }

        [Fact]
        public void Test_Create_ModuleIdentity_WithTokenProvider_AndModelId_ShouldCreateModuleClient()
        {
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = "testModelId";
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.Some(modelId));

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);
        }

        [Fact]
        public void Test_Create_DeviceIdentity_WithTokenProvider_AndModelId_ShouldCreateDeviceClient()
        {
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = "testModelId";
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.Some(modelId));

            Assert.NotNull(client);
            Assert.True(client is DeviceClientWrapper);
        }

        [Fact]
        public void Test_Create_ModuleIdentity_WithTokenProvider_AndModelId_AndGatewayHostName_ShouldCreateModuleClient()
        {
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = "testModelId";
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.Some("testGatewayHostName")).Create(identity, tokenProvider, options, Option.Some(modelId));

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);
        }

        [Fact]
        public void Test_Create_DeviceIdentity_WithTokenProvider_AndModelId_AndGatewayHostName_ShouldCreateDeviceClient()
        {
            IIdentity identity = new DeviceIdentity(IotHubHostName, DeviceId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = "testModelId";
            ITokenProvider tokenProvider = new TestTokenProvider();
            IClient client = new ClientProvider(Option.Some("testGatewayHostName")).Create(identity, tokenProvider, options, Option.Some(modelId));

            Assert.NotNull(client);
            Assert.True(client is DeviceClientWrapper);
        }

        [Fact]
        public async Task Test_Create_ModuleIdentity_WithEnv_ShouldCreateModuleClient()
        {
            Environment.SetEnvironmentVariable(IotEdgedUriVariableName, this.serverUrl.OriginalString);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, "iothub.test");
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, "localhost");
            Environment.SetEnvironmentVariable(DeviceIdVariableName, "device1");
            Environment.SetEnvironmentVariable(ModuleIdVariableName, "module1");
            Environment.SetEnvironmentVariable(ModuleGeneratioIdVariableName, "1");
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, "sasToken");

            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));

            IClient client = await new ClientProvider(Option.None<string>()).CreateAsync(identity, options);

            Assert.NotNull(client);
            Assert.True(client is ModuleClientWrapper);

            Environment.SetEnvironmentVariable(IotEdgedUriVariableName, null);
            Environment.SetEnvironmentVariable(IotHubHostnameVariableName, null);
            Environment.SetEnvironmentVariable(GatewayHostnameVariableName, null);
            Environment.SetEnvironmentVariable(DeviceIdVariableName, null);
            Environment.SetEnvironmentVariable(ModuleIdVariableName, null);
            Environment.SetEnvironmentVariable(AuthSchemeVariableName, null);
        }

        [Fact]
        public void Throw_OnWhitespaceModelId()
        {
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = "  ";
            ITokenProvider tokenProvider = new TestTokenProvider();
            Assert.Throws<ArgumentException>(() => new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.Some(modelId)));
        }

        [Fact]
        public void Throw_OnEmptyModelId()
        {
            IIdentity identity = new ModuleIdentity(IotHubHostName, DeviceId, ModuleId);

            var options = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
            string modelId = string.Empty;
            ITokenProvider tokenProvider = new TestTokenProvider();
            Assert.Throws<ArgumentException>(() => new ClientProvider(Option.None<string>()).Create(identity, tokenProvider, options, Option.Some(modelId)));
        }
    }
}
