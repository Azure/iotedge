namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Moq;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class CloudListenerTest
    {
        [Fact]
        [Unit]
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
    }
}
