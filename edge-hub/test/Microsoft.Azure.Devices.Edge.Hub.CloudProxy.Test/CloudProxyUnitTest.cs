// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CloudProxyUnitTest
    {
        [Fact]
        public async Task TestCloseOnInactive()
        {
            // Arrange
            var client = new Mock<IClient>();
            bool isClientActive = true;
            client.Setup(c => c.CloseAsync())
                .Callback(() => isClientActive = false)
                .Returns(Task.CompletedTask);
            client.SetupGet(c => c.IsActive).Returns(() => isClientActive);
            client.Setup(c => c.SendEventAsync(It.IsAny<Client.Message>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<Client.Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Client.Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Client.Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
            var message = Mock.Of<IMessage>();
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout);

            // Act
            for (int i = 0; i < 5; i++)
            {
                await cloudProxy.SendMessageAsync(message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.False(cloudProxy.IsActive);
            Assert.False(isClientActive);
            client.Verify(c => c.CloseAsync(), Times.Once);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            client.Verify(c => c.CloseAsync(), Times.Once);
        }
    }
}
