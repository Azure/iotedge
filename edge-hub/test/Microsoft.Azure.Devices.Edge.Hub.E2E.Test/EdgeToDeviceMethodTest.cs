// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [E2E]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class EdgeToDeviceMethodTest : IClassFixture<ProtocolHeadFixture>
    {
        static readonly ITransportSettings[] MqttTransportSettings =
        {
            new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        static readonly ITransportSettings[] AmqpTransportSettings =
        {
            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        const string DeviceNamePrefix = "E2E_DirectMethods_";
        const string MethodName = "WriteToConsole";

        static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);
        static readonly string GatewayDeviceId = ConfigHelper.TestConfig["GatewayDeviceId"];
        static readonly string ModuleId = $"{GatewayDeviceId}/module1";

        [Fact, TestPriority(301)]
        public Task InvokeMethodModuleTest_Amqp() => this.InvokeMethodOnModuleTest(AmqpTransportSettings);

        [Fact, TestPriority(302)]
        public Task InvokeMethodModuleTest_Mqtt() => this.InvokeMethodOnModuleTest(MqttTransportSettings);

        async Task InvokeMethodOnModuleTest(ITransportSettings[] transportSettings)
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);
            ServiceClient sender = null;
            DeviceClient receiver = null;

            var request = new TestMethodRequest("Prop1", 10);
            var response = new TestMethodResponse("RespProp1", 20);
            TestMethodRequest receivedRequest = null;
            Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object context)
            {
                receivedRequest = JsonConvert.DeserializeObject<TestMethodRequest>(methodRequest.DataAsJson);
                return Task.FromResult(new MethodResponse(
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), 200));
            }

            try
            {
                string receiverModuleName = "receiver1";
                sender = ServiceClient.CreateFromConnectionString(iotHubConnectionString);

                string receiverModuleConnectionString = await RegistryManagerHelper.CreateModuleIfNotExists(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, receiverModuleName);
                receiver = DeviceClient.CreateFromConnectionString(receiverModuleConnectionString, transportSettings);
                await receiver.OpenAsync();
                await receiver.SetMethodHandlerAsync("poke", MethodHandler, null);

                // Act
                CloudToDeviceMethodResult cloudToDeviceMethodResult = await sender.InvokeDeviceMethodAsync(
                    connectionStringBuilder.DeviceId,
                    receiverModuleName,
                    new CloudToDeviceMethod("poke").SetPayloadJson(JsonConvert.SerializeObject(request)));

                // Assert
                Assert.NotNull(cloudToDeviceMethodResult);
                Assert.NotNull(receivedRequest);
                Assert.Equal(receivedRequest.RequestProp1, request.RequestProp1);
                Assert.Equal(receivedRequest.RequestProp2, request.RequestProp2);

                Assert.Equal(200, cloudToDeviceMethodResult.Status);
                TestMethodResponse receivedResponse = JsonConvert.DeserializeObject<TestMethodResponse>(cloudToDeviceMethodResult.GetPayloadAsJson());
                Assert.NotNull(receivedResponse);
                Assert.Equal(receivedResponse.ResponseProp1, response.ResponseProp1);
                Assert.Equal(receivedResponse.ResponseProp2, response.ResponseProp2);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
                if (receiver != null)
                {
                    await receiver.CloseAsync();
                }
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        [Fact, TestPriority(303)]
        public Task InvokeMethodDeviceTest_Amqp() => this.InvokeMethodOnDeviceTest(AmqpTransportSettings);

        [Fact, TestPriority(304)]
        public Task InvokeMethodDeviceTest_Mqtt() => this.InvokeMethodOnDeviceTest(MqttTransportSettings);

        async Task InvokeMethodOnDeviceTest(ITransportSettings[] transportSettings)
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            ServiceClient sender = null;
            DeviceClient receiver = null;

            var request = new TestMethodRequest("Prop1", 10);
            var response = new TestMethodResponse("RespProp1", 20);
            TestMethodRequest receivedRequest = null;
            Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object context)
            {
                receivedRequest = JsonConvert.DeserializeObject<TestMethodRequest>(methodRequest.DataAsJson);
                return Task.FromResult(new MethodResponse(
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), 200));
            }

            try
            {
                sender = ServiceClient.CreateFromConnectionString(iotHubConnectionString);

                (string deviceId, string receiverModuleConnectionString) = await RegistryManagerHelper.CreateDevice("methodTest", iotHubConnectionString, rm, false, true);
                receiver = DeviceClient.CreateFromConnectionString(receiverModuleConnectionString, transportSettings);
                await receiver.OpenAsync();
                await receiver.SetMethodHandlerAsync("poke", MethodHandler, null);

                // Act
                CloudToDeviceMethodResult cloudToDeviceMethodResult = await sender.InvokeDeviceMethodAsync(
                    deviceId,
                    new CloudToDeviceMethod("poke").SetPayloadJson(JsonConvert.SerializeObject(request)));

                // Assert
                Assert.NotNull(cloudToDeviceMethodResult);
                Assert.NotNull(receivedRequest);
                Assert.Equal(receivedRequest.RequestProp1, request.RequestProp1);
                Assert.Equal(receivedRequest.RequestProp2, request.RequestProp2);

                Assert.Equal(200, cloudToDeviceMethodResult.Status);
                TestMethodResponse receivedResponse = JsonConvert.DeserializeObject<TestMethodResponse>(cloudToDeviceMethodResult.GetPayloadAsJson());
                Assert.NotNull(receivedResponse);
                Assert.Equal(receivedResponse.ResponseProp1, response.ResponseProp1);
                Assert.Equal(receivedResponse.ResponseProp2, response.ResponseProp2);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
                if (receiver != null)
                {
                    await receiver.CloseAsync();
                }
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
        
        async Task<DeviceClient> ConnectToEdge(string connectionString)
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            var settings = new ITransportSettings[1];
            settings[0] = mqttSetting;

            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await client.OpenAsync();

            return client;
        }

        class TestMethodRequest
        {
            [JsonConstructor]
            public TestMethodRequest(string requestProp1, int requestProp2)
            {
                RequestProp1 = requestProp1;
                RequestProp2 = requestProp2;
            }

            [JsonProperty("requestProp1")]
            public string RequestProp1 { get; }

            [JsonProperty("requestProp2")]
            public int RequestProp2 { get; }
        }

        class TestMethodResponse
        {
            [JsonConstructor]
            public TestMethodResponse(string responseProp1, int responseProp2)
            {
                ResponseProp1 = responseProp1;
                ResponseProp2 = responseProp2;
            }

            [JsonProperty("responseProp1")]
            public string ResponseProp1 { get; }

            [JsonProperty("responseProp2")]
            public int ResponseProp2 { get; }
        }
    }
}
