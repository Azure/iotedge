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

    [Bvt]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class EdgeToDeviceMethodTest
    {
        ProtocolHeadFixture head = ProtocolHeadFixture.GetInstance();
        const string ModuleId = "device1/module1";
        const string Device2Id = "device2";
        const string Device3Id = "device3";
        const string Device4Id = "device4";
        const string MethodName = "WriteToConsole";
        const string DataAsJson = "{\"MethodPayload\":\"Payload\"}";
        const int MethodNotFoundStatus = 501;
        const int MethodOkStatus = 200;
        static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(201)]
        public async Task Receive_DirectMethodCall_Module_WhenRegistered_ShouldCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");

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

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(202)]
        public async Task Receive_DirectMethodCall_Module_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");

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

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        [Fact(Skip = "Module not implemented by service or device SDK - SWITCH APIs from Device to Module"), TestPriority(203)]
        public async Task Receive_DirectMethodCall_Module_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");
            var callback = new Mock<MethodCallback>();

            // Invoke the direct method from the local service
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                ModuleId,
                new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
            );

            // Validate results
            await Assert.ThrowsAsync<Common.Exceptions.DeviceNotFoundException>(() => directResponseFuture);
            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        [Fact, TestPriority(204)]
        public async Task Receive_DirectMethodCall_Device_WhenRegistered_ShouldCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice2ConnStr1");

            // Setup mocked callback for direct method call
            var callback = new Mock<MethodCallback>();
            callback.Setup(f => f(It.IsAny<MethodRequest>(), null))
                .Returns(Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(DataAsJson), MethodOkStatus)));

            // Set the callback to be invoked upon direct method request
            await client.SetMethodHandlerAsync(MethodName, callback.Object, null);

            // Invoke the direct method from the local service
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                Device2Id,
                new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
            );

            // Await the response from the client
            CloudToDeviceMethodResult response = await directResponseFuture;

            // Validate results
            Assert.Equal(MethodOkStatus, response.Status);
            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Once());

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        [Fact, TestPriority(205)]
        public async Task Receive_DirectMethodCall_Device_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice3ConnStr1");

            // Setup mocked callback for direct method call
            var callback = new Mock<MethodCallback>();

            // Set the callback to be invoked upon direct method request
            await client.SetMethodHandlerAsync(MethodName + "1", callback.Object, null);

            // Invoke the direct method from the local service
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                Device3Id,
                new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
            );

            // Await the response from the client
            CloudToDeviceMethodResult response = await directResponseFuture;

            // Validate results
            Assert.Equal(MethodNotFoundStatus, response.Status);
            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        [Fact, TestPriority(206)]
        public async Task Receive_DirectMethodCall_Device_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            // Get the connection string from the secret store
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

            // Create a service client using the IoT Hub connection string (local service)
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            await serviceClient.OpenAsync();

            // Connect to the Edge Hub and create the device client
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice4ConnStr1");

            // Setup mocked callback for direct method call
            var callback = new Mock<MethodCallback>();

            // Invoke the direct method from the local service
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync(
                Device4Id,
                new CloudToDeviceMethod(MethodName, ResponseTimeout).SetPayloadJson(DataAsJson)
            );

            // Validate results
            await Assert.ThrowsAsync<Common.Exceptions.DeviceNotFoundException>(() => directResponseFuture);
            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());

            // Clean-up resources
            await serviceClient.CloseAsync();
            await client.CloseAsync();
        }

        async Task<DeviceClient> ConnectToEdge(string connectionStringSecretName)
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            var settings = new ITransportSettings[1];
            settings[0] = mqttSetting;

            string connectionString = await SecretsHelper.GetSecret(connectionStringSecretName);
            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await client.OpenAsync();

            return client;
        }
    }
}
