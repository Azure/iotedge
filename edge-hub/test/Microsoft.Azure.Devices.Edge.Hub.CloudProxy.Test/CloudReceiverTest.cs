// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class CloudReceiverTest
    {
        const string MethodName = "MethodName";
        const string RequestId = "1";
        const int StatusCode = 200;
        static readonly byte[] Data = new byte[0];

        [Fact]
        [Integration]
        public async Task MethodCallHandler_WhenResponse_WithRequestIdReceived_Completes()
        {
            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(p => p.CallMethodAsync(It.IsAny<DirectMethodRequest>())).Returns(Task.FromResult(new DirectMethodResponse(RequestId, Data, StatusCode)));
            var messageConverter = new Mock<IMessageConverterProvider>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");

            var deviceClient = Mock.Of<IDeviceClient>();
            var cloudProxy = new CloudProxy(deviceClient, messageConverter.Object, identity.Id, (id, s) => { });

            var cloudReceiver = new CloudProxy.CloudReceiver(cloudProxy, cloudListener.Object);

            MethodResponse methodResponse = await cloudReceiver.MethodCallHandler(new MethodRequest(MethodName, Data), null);
            cloudListener.Verify(p => p.CallMethodAsync(It.Is<DirectMethodRequest>(x => x.Name == MethodName && x.Data == Data)), Times.Once);
            Assert.NotNull(methodResponse);
        }
    }
}
