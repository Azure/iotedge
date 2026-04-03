// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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
            client.Setup(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<TelemetryMessage>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new TelemetryMessage(new byte[0]));

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<TelemetryMessage>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
            TimeSpan cloudConnectionHangingTimeout = TimeSpan.FromSeconds(50);
            var message = new EdgeMessage(new byte[0], new Dictionary<string, string>(), new Dictionary<string, string>());
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true, cloudConnectionHangingTimeout);

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
            client.Setup(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<TelemetryMessage>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new TelemetryMessage(new byte[0]));

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<TelemetryMessage>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
            TimeSpan cloudConnectionHangingTimeout = TimeSpan.FromSeconds(50);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, false, cloudConnectionHangingTimeout);

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
            client.Setup(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>())).Returns(Task.CompletedTask);
            client.Setup(c => c.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<Func<PropertyCollection, Task>>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<TelemetryMessage>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new TelemetryMessage(new byte[0]));

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<TelemetryMessage>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            TimeSpan cloudConnectionHangingTimeout = TimeSpan.FromSeconds(50);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true, cloudConnectionHangingTimeout);

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
            client.Setup(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>())).Returns(Task.CompletedTask);
            client.Setup(c => c.SetDirectMethodCallbackAsync(It.IsAny<Func<Client.DirectMethodRequest, Task<Client.DirectMethodResponse>>>())).Returns(Task.CompletedTask);

            var messageConverter = new Mock<IMessageConverter<TelemetryMessage>>();
            messageConverter.Setup(m => m.FromMessage(It.IsAny<IMessage>()))
                .Returns(new TelemetryMessage(new byte[0]));

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<TelemetryMessage>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            TimeSpan cloudConnectionHangingTimeout = TimeSpan.FromSeconds(50);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true, cloudConnectionHangingTimeout);

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
                .Returns(Task.FromResult<IncomingMessage>(new IncomingMessage(new byte[0])));

            var messageConverter = new Mock<IMessageConverter<IncomingMessage>>();
            messageConverter.Setup(m => m.ToMessage(It.IsAny<IncomingMessage>()))
                .Returns(new EdgeMessage(new byte[0], new Dictionary<string, string>(), new Dictionary<string, string>()));

            var messageConverterProvider = new Mock<IMessageConverterProvider>();
            messageConverterProvider.Setup(m => m.Get<IncomingMessage>())
                .Returns(messageConverter.Object);

            var cloudListener = new Mock<ICloudListener>();
            cloudListener.Setup(c => c.ProcessMessageAsync(It.IsAny<IMessage>())).ThrowsAsync(new InvalidOperationException());
            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            TimeSpan cloudConnectionHangingTimeout = TimeSpan.FromSeconds(50);
            ICloudProxy cloudProxy = new CloudProxy(client.Object, messageConverterProvider.Object, "device1", null, cloudListener.Object, idleTimeout, true, cloudConnectionHangingTimeout);

            // Act
            await cloudProxy.StartListening();

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
