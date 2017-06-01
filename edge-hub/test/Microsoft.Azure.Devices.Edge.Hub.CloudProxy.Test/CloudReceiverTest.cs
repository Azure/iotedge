// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;
    using Xunit;

    public class CloudReceiverTest
    {
        const string MethodName = "MethodName";
        const string InvalidRequestId = "100";
        const string RequestId = "1";
        const int StatusCode = 200;
        static readonly byte[] Data = new byte[0];

        [Fact]
        public async Task MethodCallHandler_WhenResponse_WithRequestIdReceived_Completes()
        {
            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(p => p.CallMethodAsync(It.IsAny<DirectMethodRequest>())).Returns(TaskEx.Done);
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = new Mock<IIdentity>();

            string key = Convert.ToBase64String(Encoding.UTF8.GetBytes("token"));
            DeviceClient deviceClient = DeviceClient.Create("127.0.0.1", new DeviceAuthenticationWithRegistrySymmetricKey("device1", key));

            var cloudReceiver = new CloudReceiver(deviceClient, messageConverter.Object, cloudListener.Object, identity.Object);

            Task<MethodResponse> task = cloudReceiver.MethodCallHandler(new MethodRequest(MethodName, Data), null);

            cloudListener.Verify(p => p.CallMethodAsync(It.Is<DirectMethodRequest>(x => x.Id == RequestId && x.Name == MethodName && x.Data == Data)), Times.Once);
            Assert.False(task.IsCompleted);
            await cloudReceiver.SendMethodResponseAsync(new DirectMethodResponse(RequestId, Data, StatusCode));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task MethodCallHandler_WhenResponse_WithOtherRequestIdReceived_DoesNotComplete()
        {
            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(p => p.CallMethodAsync(It.IsAny<DirectMethodRequest>())).Returns(TaskEx.Done);
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = new Mock<IIdentity>();

            string key = Convert.ToBase64String(Encoding.UTF8.GetBytes("token"));
            DeviceClient deviceClient = DeviceClient.Create("127.0.0.1", new DeviceAuthenticationWithRegistrySymmetricKey("device1", key));

            var cloudReceiver = new CloudReceiver(deviceClient, messageConverter.Object, cloudListener.Object, identity.Object);

            Task<MethodResponse> task = cloudReceiver.MethodCallHandler(new MethodRequest(MethodName, Data), null);

            cloudListener.Verify(p => p.CallMethodAsync(It.Is<DirectMethodRequest>(x => x.Id == RequestId && x.Name == MethodName && x.Data == Data)), Times.Once);
            Assert.False(task.IsCompleted);
            await cloudReceiver.SendMethodResponseAsync(new DirectMethodResponse(InvalidRequestId, Data, StatusCode));
            Assert.False(task.IsCompleted);
        }
    }
}
