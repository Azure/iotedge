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
    using Moq;
    using Xunit;

    [E2E]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class EdgeToDeviceMethodTest : IClassFixture<ProtocolHeadFixture>
    {
        const string DeviceNamePrefix = "E2E_DirectMethods_";
        const string MethodName = "WriteToConsole";
        const string DataAsJson = "{\"MethodPayload\":\"Payload\"}";
        const int MethodNotFoundStatus = 501;
        const int MethodOkStatus = 200;
        static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);
        static readonly string GatewayDeviceId = ConfigHelper.TestConfig["GatewayDeviceId"];
        static readonly string ModuleId = $"{GatewayDeviceId}/module1";

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(201)]
        public async Task Receive_DirectMethodCall_Module_WhenRegistered_ShouldCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient client = null;

            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                string connectionString = await SecretsHelper.GetSecretFromConfigKey("iotEdgeModuleConnStrKey");
                client = await this.ConnectToEdge(connectionString);

                // Setup mocked callback for direct method call
                var callback = new Mock<MethodCallback>();
                callback.Setup(f => f(It.IsAny<MethodRequest>(), It.IsAny<object>()))
                    .Returns(Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(DataAsJson), MethodOkStatus)));

                // Set the callback to be invoked upon direct method request
                await client.SetMethodDefaultHandlerAsync(callback.Object, null);

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    ModuleId,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Await the response from the client
                CloudToDeviceMethodResult response = await directResponseFuture;

                // Validate results
                Assert.Equal(MethodOkStatus, response.Status);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Once());
            }
            finally
            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
            }
        }

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(202)]
        public async Task Receive_DirectMethodCall_Module_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient client = null;

            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                string connectionString = await SecretsHelper.GetSecretFromConfigKey("iotEdgeModuleConnStrKey");
                client = await this.ConnectToEdge(connectionString);

                // Setup mocked callback for direct method call
                var callback = new Mock<MethodCallback>();

                // Set the callback to be invoked upon direct method request
                await client.SetMethodHandlerAsync(MethodName + "1", callback.Object, null);

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    ModuleId,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Await the response from the client
                CloudToDeviceMethodResult response = await directResponseFuture;

                // Validate results
                Assert.Equal(MethodNotFoundStatus, response.Status);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            }
            finally
            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
            }
        }

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(203)]
        public async Task Receive_DirectMethodCall_Module_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient client = null;

            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                string connectionString = await SecretsHelper.GetSecretFromConfigKey("iotEdgeModuleConnStrKey");
                client = await this.ConnectToEdge(connectionString);
                var callback = new Mock<MethodCallback>();

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    ModuleId,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Validate results
                await Assert.ThrowsAsync<Common.Exceptions.DeviceNotFoundException>(() => directResponseFuture);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            }
            finally

            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
            }
        }

        [Fact, TestPriority(204)]
        public async Task Receive_DirectMethodCall_Device_WhenRegistered_ShouldCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm);

            ServiceClient serviceClient = null;
            DeviceClient client = null;
            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                client = await this.ConnectToEdge(deviceConnectionString);

                // Setup mocked callback for direct method call
                var callback = new Mock<MethodCallback>();
                callback.Setup(f => f(It.IsAny<MethodRequest>(), null))
                    .Returns(Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(DataAsJson), MethodOkStatus)));

                // Set the callback to be invoked upon direct method request
                await client.SetMethodHandlerAsync(MethodName, callback.Object, null);

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    deviceName,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Await the response from the client
                CloudToDeviceMethodResult response = await directResponseFuture;

                // Validate results
                Assert.Equal(MethodOkStatus, response.Status);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Once());
            }
            finally
            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
                if (rm != null)
                {
                    await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                    await rm.CloseAsync();
                }
            }
        }

        [Fact, TestPriority(205)]
        public async Task Receive_DirectMethodCall_Device_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm);

            ServiceClient serviceClient = null;
            DeviceClient client = null;
            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                client = await this.ConnectToEdge(deviceConnectionString);

                // Setup mocked callback for direct method call
                var callback = new Mock<MethodCallback>();

                // Set the callback to be invoked upon direct method request
                await client.SetMethodHandlerAsync(MethodName + "1", callback.Object, null);

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    deviceName,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Await the response from the client
                CloudToDeviceMethodResult response = await directResponseFuture;

                // Validate results
                Assert.Equal(MethodNotFoundStatus, response.Status);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            }
            finally
            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
                if (rm != null)
                {
                    await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                    await rm.CloseAsync();
                }
            }
        }

        [Fact, TestPriority(206)]
        public async Task Receive_DirectMethodCall_Device_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm);

            ServiceClient serviceClient = null;
            DeviceClient client = null;
            try
            {
                // Create a service client using the IoT Hub connection string (local service)
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Connect to the Edge Hub and create the device client
                client = await this.ConnectToEdge(deviceConnectionString);

                // Setup mocked callback for direct method call
                var callback = new Mock<MethodCallback>();

                // Invoke the direct method from the local service
                Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                    deviceName,
                    new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
                );

                // Validate results
                await Assert.ThrowsAsync<Common.Exceptions.DeviceNotFoundException>(() => directResponseFuture);
                callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            }
            finally
            {
                // Clean-up resources
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
                if (client != null)
                {
                    await client.CloseAsync();
                }
                if (rm != null)
                {
                    await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                    await rm.CloseAsync();
                }
            }
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
    }
}
