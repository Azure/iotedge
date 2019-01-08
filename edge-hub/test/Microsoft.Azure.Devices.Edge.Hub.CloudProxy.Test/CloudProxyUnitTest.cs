// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
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
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
            var message = Mock.Of<IMessage>();
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true);

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
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Assert
            client.Verify(c => c.CloseAsync(), Times.Once);
        }

        [Fact]
        public async Task TestCloseOnInactiveDisabled()
        {
            // Arrange
            var client = new Mock<IClient>();
            bool isClientActive = true;
            client.Setup(c => c.CloseAsync())
                .Callback(() => isClientActive = false)
                .Returns(Task.CompletedTask);
            client.SetupGet(c => c.IsActive).Returns(() => isClientActive);
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, false);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);
            client.Verify(c => c.CloseAsync(), Times.Never);
        }

        [Fact]
        public async Task TestDisableOnDesiredPropertiesSubscription()
        {
            // Arrange
            var client = new Mock<IClient>();
            bool isClientActive = true;
            client.Setup(c => c.CloseAsync())
                .Callback(() => isClientActive = false)
                .Returns(Task.CompletedTask);
            client.SetupGet(c => c.IsActive).Returns(() => isClientActive);
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).Returns(Task.CompletedTask);
            client.Setup(c => c.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>(), It.IsAny<object>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true);

            // Act
            await cloudProxy.SetupDesiredPropertyUpdatesAsync();

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);
            client.Verify(c => c.CloseAsync(), Times.Never);
        }

        [Fact]
        public async Task TestDisableOnMethodsSubscription()
        {
            // Arrange
            var client = new Mock<IClient>();
            bool isClientActive = true;
            client.Setup(c => c.CloseAsync())
                .Callback(() => isClientActive = false)
                .Returns(Task.CompletedTask);
            client.SetupGet(c => c.IsActive).Returns(() => isClientActive);
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).Returns(Task.CompletedTask);
            client.Setup(c => c.SetMethodDefaultHandlerAsync(It.IsAny<MethodCallback>(), It.IsAny<object>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true);

            // Act
            await cloudProxy.SetupCallMethodAsync();

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);
            client.Verify(c => c.CloseAsync(), Times.Never);
        }

        [Fact]
        public async Task TestDisableTimerOnC2DSubscription()
        {
            // Arrange
            var client = new Mock<IClient>();
            bool isClientActive = true;
            client.Setup(c => c.CloseAsync())
                .Callback(() => isClientActive = false)
                .Returns(Task.CompletedTask);
            client.SetupGet(c => c.IsActive).Returns(() => isClientActive);
            client.Setup(c => c.ReceiveAsync(It.IsAny<TimeSpan>()))
                // .Callback<TimeSpan>(t => Task.Yield())
                .Returns(Task.FromResult<Message>(new Message()));

            var messageConverter = new Mock<IMessageConverter<Message>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new Message());

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<Message>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(c => c.ProcessMessageAsync(It.IsAny<IMessage>())).ThrowsAsync(new InvalidOperationException());
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true);

            // Act
            cloudProxy.StartListening();

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(cloudProxy.IsActive);
            Assert.True(isClientActive);
            client.Verify(c => c.CloseAsync(), Times.Never);
        }
    }
}
