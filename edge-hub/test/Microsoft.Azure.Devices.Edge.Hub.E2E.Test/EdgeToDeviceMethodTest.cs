// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Bvt]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class EdgeToDeviceMethodTest : IClassFixture<MockedMqttHeadFixture>
    {
        readonly IConnectionManager connectionManager;
        readonly Mock<IDeviceListener> deviceListener;

        const string ModuleId = "device1/module1";
        const string Device2Id = "device2";
        const string Device3Id = "device3";
        const string Device4Id = "device4";
        const string MethodName = "WriteToConsole";
        const string DataAsJson = "{\"MethodPayload\": \"Payload\" }";
        const int MethodNotFoundStatus = 501;
        const int MethodOkStatus = 200;
        static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);

        public EdgeToDeviceMethodTest(MockedMqttHeadFixture fixture)
        {
            this.connectionManager = fixture.ConnectionManager;
            this.deviceListener = fixture.DeviceListener;
        }

        [Fact, TestPriority(1)]
        public async Task Receive_DirectMethodCall_Module_WhenRegistered_ShouldCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");
            var callback = new Mock<MethodCallback>();
            callback.Setup(f => f(It.IsAny<MethodRequest>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(DataAsJson), MethodOkStatus)));
            await client.SetMethodDefaultHandlerAsync(callback.Object, null);

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(ModuleId, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);
            this.connectionManager.GetDeviceConnection(ModuleId).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Once());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.Is<DirectMethodResponse>(r => r.Status == MethodOkStatus && r.CorrelationId == methodRequest.CorrelationId)), Times.Once());

            await client.CloseAsync();
        }

        [Fact, TestPriority(2)]
        public async Task Receive_DirectMethodCall_Module_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");
            var callback = new Mock<MethodCallback>();
            await client.SetMethodHandlerAsync(MethodName + "1", callback.Object, null);

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(ModuleId, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);
            this.connectionManager.GetDeviceConnection(ModuleId).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.Is<DirectMethodResponse>(r => r.Status == MethodNotFoundStatus && r.CorrelationId == methodRequest.CorrelationId)), Times.Once());

            await client.CloseAsync();
        }

        [Fact, TestPriority(3)]
        public async Task Receive_DirectMethodCall_Module_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeModuleConnStr1");
            var callback = new Mock<MethodCallback>();

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(ModuleId, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);
            this.connectionManager.GetDeviceConnection(ModuleId).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.IsAny<DirectMethodResponse>()), Times.Never);

            await client.CloseAsync();
        }

        [Fact, TestPriority(4)]
        public async Task Receive_DirectMethodCall_Device_WhenRegistered_ShouldCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice2ConnStr1");
            var callback = new Mock<MethodCallback>();
            callback.Setup(f => f(It.IsAny<MethodRequest>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(DataAsJson), MethodOkStatus)));
            await client.SetMethodHandlerAsync(MethodName, callback.Object, null);

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(Device2Id, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);

            this.connectionManager.GetDeviceConnection(Device2Id).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Once());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.Is<DirectMethodResponse>(r => r.Status == MethodOkStatus && r.CorrelationId == methodRequest.CorrelationId)), Times.Once());

            await client.CloseAsync();
        }

        [Fact, TestPriority(5)]
        public async Task Receive_DirectMethodCall_Device_WhenOtherMethodRegistered_ShouldNotCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice3ConnStr1");
            var callback = new Mock<MethodCallback>();
            await client.SetMethodHandlerAsync(MethodName + "1", callback.Object, null);

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(Device3Id, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);
            this.connectionManager.GetDeviceConnection(Device3Id).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.Is<DirectMethodResponse>(r => r.Status == MethodNotFoundStatus && r.CorrelationId == methodRequest.CorrelationId)), Times.Once());

            await client.CloseAsync();
        }

        [Fact, TestPriority(6)]
        public async Task Receive_DirectMethodCall_Device_WhenNoMethodRegistered_ShouldNotCallHandler()
        {
            DeviceClient client = await this.ConnectToEdge("IotEdgeDevice4ConnStr1");
            var callback = new Mock<MethodCallback>();

            this.deviceListener.ResetCalls();
            var methodRequest = new DirectMethodRequest(Device4Id, MethodName, Encoding.UTF8.GetBytes(DataAsJson), ResponseTimeout);
            this.connectionManager.GetDeviceConnection(Device4Id).Map(async dp => await dp.CallMethodAsync(methodRequest));

            // needs delay to get the response from the client
            await Task.Delay(1000);

            callback.Verify(f => f(It.Is<MethodRequest>(request => request.Name == MethodName && request.DataAsJson == DataAsJson), It.IsAny<object>()), Times.Never());
            this.deviceListener.Verify(p => p.ProcessMethodResponseAsync(It.IsAny<DirectMethodResponse>()), Times.Never);

            await client.CloseAsync();
        }

        async Task<DeviceClient> ConnectToEdge(string connectionStringSecretName)
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            var settings = new ITransportSettings[1];
            settings[0] = mqttSetting;

            string connectionString = await SecretsHelper.GetSecret(connectionStringSecretName);
            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await client.OpenAsync();

            return client;
        }
    }
}
