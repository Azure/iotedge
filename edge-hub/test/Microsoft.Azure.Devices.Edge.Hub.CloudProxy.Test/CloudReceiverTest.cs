// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
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
            cloudListener.Setup(p => p.CallMethodAsync(It.IsAny<DirectMethodRequest>())).Returns(Task.FromResult(new DirectMethodResponse(RequestId, Data, StatusCode)));
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = new Mock<IIdentity>();

            string key = Convert.ToBase64String(Encoding.UTF8.GetBytes("token"));
            DeviceClient deviceClient = DeviceClient.Create("127.0.0.1", new DeviceAuthenticationWithRegistrySymmetricKey("device1", key));

            var cloudReceiver = new CloudReceiver(deviceClient, messageConverter.Object, cloudListener.Object, identity.Object);

            MethodResponse methodResponse = await cloudReceiver.MethodCallHandler(new MethodRequest(MethodName, Data), null);
            cloudListener.Verify(p => p.CallMethodAsync(It.Is<DirectMethodRequest>(x => x.Name == MethodName && x.Data == Data)), Times.Once);
            Assert.NotNull(methodResponse);
        }        
    }
}
