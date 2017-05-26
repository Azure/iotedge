namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Moq;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Unit]
    public class CloudListenerTest
    {
        [Fact]
        public async Task TestProcessMessage()
        {
            var deviceProxy = new Mock<IDeviceProxy>();

            IMessage sentMessage = null;
            deviceProxy.Setup(r => r.SendMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.FromResult(true))
                .Callback<IMessage>(m => sentMessage = m);


            var cloudListener = new CloudListener(deviceProxy.Object);

            var payload = new byte[] {1, 2, 3};
            IMessage message = new Message(payload);
            await cloudListener.ProcessMessageAsync(message);

            Assert.NotNull(sentMessage);
        }

        [Fact]
        public async Task OnDesiredPropertyUpdatesForwardsToDeviceProxy()
        {
            string actual = null;
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(r => r.OnDesiredPropertyUpdates(It.IsAny<string>()))
                .Callback<string>(s => actual = s)
                .Returns(Task.FromResult(true));

            string expected = "{\"abc\":\"xyz\"}";
            var cloudListener = new CloudListener(deviceProxy.Object);
            await cloudListener.OnDesiredPropertyUpdates(expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CallMethodAsync_InvokesDeviceProxy()
        {
            var deviceProxy = Mock.Of<IDeviceProxy>();
            string testMethod = "testMethod";
            var testByteArray = new byte[] { 0x00, 0x01, 0x02 };

            var cloudListener = new CloudListener(deviceProxy);
            await cloudListener.CallMethodAsync(testMethod, testByteArray);
            Mock.Get(deviceProxy).Verify(dp => dp.CallMethodAsync(testMethod, testByteArray), Times.Once);
        }
    }
}
