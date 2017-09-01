namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CloudListenerTest
    {
        [Fact]
        public async Task TestProcessMessage()
        {
            var deviceProxy = new Mock<IDeviceProxy>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IIdentity>();

            IMessage sentMessage = null;
            deviceProxy.Setup(r => r.SendC2DMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.FromResult(true))
                .Callback<IMessage>(m => sentMessage = m);


            var cloudListener = new CloudListener(deviceProxy.Object, edgeHub, identity);

            var payload = new byte[] {1, 2, 3};
            IMessage message = new Message(payload);
            await cloudListener.ProcessMessageAsync(message);

            Assert.NotNull(sentMessage);
        }

        [Fact]
        public async Task OnDesiredPropertyUpdatesForwardsToDeviceProxy()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IIdentity>();
            IMessage actual = null;
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(r => r.OnDesiredPropertyUpdates(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => actual = m)
                .Returns(Task.FromResult(true));

            var expected = new Message(Encoding.UTF8.GetBytes("{\"abc\":\"xyz\"}"));
            var cloudListener = new CloudListener(deviceProxy.Object, edgeHub, identity);
            await cloudListener.OnDesiredPropertyUpdates(expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CallMethodAsync_InvokesDeviceProxy()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IIdentity>();
            var deviceProxy = Mock.Of<IDeviceProxy>();
            string testMethod = "testMethod";
            var testByteArray = new byte[] { 0x00, 0x01, 0x02 };
            string id = "1";
            var request = new DirectMethodRequest(id, testMethod, testByteArray, TimeSpan.FromSeconds(30));

            var cloudListener = new CloudListener(deviceProxy, edgeHub, identity);
            await cloudListener.CallMethodAsync(request);
            Mock.Get(deviceProxy).Verify(dp => dp.InvokeMethodAsync(request), Times.Once);
        }
    }
}
