namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CloudListenerTest
    {
        [Fact]
        public async Task TestProcessMessage()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");

            IMessage sentMessage = null;
            edgeHub.Setup(r => r.SendC2DMessageAsync(It.IsAny<string>(), It.IsAny<IMessage>()))
                .Returns(Task.FromResult(true))
                .Callback<string, IMessage>((id, m) => sentMessage = m);

            var cloudListener = new CloudListener(edgeHub.Object, identity.Id);

            var payload = new byte[] { 1, 2, 3 };
            IMessage message = new EdgeMessage.Builder(payload).Build();
            await cloudListener.ProcessMessageAsync(message);

            Assert.NotNull(sentMessage);
        }

        [Fact]
        public async Task OnDesiredPropertyUpdatesForwardsToEdgeHub()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            IMessage actual = null;
            edgeHub.Setup(r => r.UpdateDesiredPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>()))
                .Callback<string, IMessage>((s, m) => actual = m)
                .Returns(Task.FromResult(true));

            IMessage expected = new EdgeMessage.Builder(Encoding.UTF8.GetBytes("{\"abc\":\"xyz\"}")).Build();
            var cloudListener = new CloudListener(edgeHub.Object, identity.Id);
            await cloudListener.OnDesiredPropertyUpdates(expected);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CallMethodAsync_InvokesDeviceProxy()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "device1");
            string testMethod = "testMethod";
            var testByteArray = new byte[] { 0x00, 0x01, 0x02 };
            string id = "1";
            var request = new DirectMethodRequest(id, testMethod, testByteArray, TimeSpan.FromSeconds(30));

            var cloudListener = new CloudListener(edgeHub, identity.Id);
            await cloudListener.CallMethodAsync(request);
            Mock.Get(edgeHub).Verify(eh => eh.InvokeMethodAsync(identity.Id, request), Times.Once);
        }
    }
}
