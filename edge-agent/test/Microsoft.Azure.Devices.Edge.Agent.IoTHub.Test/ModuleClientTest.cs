// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using ModuleClient = Microsoft.Azure.Devices.Edge.Agent.IoTHub.ModuleClient;

    [Unit]
    public class ModuleClientTest
    {
        [Fact]
        public async Task CloseOnInactivityTest()
        {
            // Arrange
            var sdkModuleClient = new Mock<ISdkModuleClient>(MockBehavior.Strict);
            sdkModuleClient.Setup(s => s.CloseAsync())
                .Returns(Task.CompletedTask);

            TimeSpan idleTimeout = TimeSpan.FromSeconds(2);
            bool closeOnIdleTimeout = true;

            // Act
            var moduleClient = new ModuleClient(sdkModuleClient.Object, idleTimeout, closeOnIdleTimeout, Core.UpstreamProtocol.AmqpWs);
            Assert.Equal(Core.UpstreamProtocol.AmqpWs, moduleClient.UpstreamProtocol);

            // Assert
            Assert.True(moduleClient.IsActive);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.False(moduleClient.IsActive);
            sdkModuleClient.Verify(s => s.CloseAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseOnInactivityGetsResetTest()
        {
            // Arrange
            MethodCallback testMethodCallback = (request, context) => Task.FromResult(new MethodResponse(200));

            var sdkModuleClient = new Mock<ISdkModuleClient>(MockBehavior.Strict);
            sdkModuleClient.Setup(s => s.CloseAsync())
                .Returns(Task.CompletedTask);
            sdkModuleClient.Setup(s => s.SetDefaultMethodHandlerAsync(testMethodCallback))
                .Returns(Task.CompletedTask);

            TimeSpan idleTimeout = TimeSpan.FromSeconds(3);
            bool closeOnIdleTimeout = true;

            // Act
            var moduleClient = new ModuleClient(sdkModuleClient.Object, idleTimeout, closeOnIdleTimeout, Core.UpstreamProtocol.Amqp);
            Assert.True(moduleClient.IsActive);
            Assert.Equal(Core.UpstreamProtocol.Amqp, moduleClient.UpstreamProtocol);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(2));
            await moduleClient.SetDefaultMethodHandlerAsync(testMethodCallback);
            Assert.True(moduleClient.IsActive);

            await Task.Delay(TimeSpan.FromSeconds(2));
            await moduleClient.SetDefaultMethodHandlerAsync(testMethodCallback);
            Assert.True(moduleClient.IsActive);

            await Task.Delay(TimeSpan.FromSeconds(2));
            await moduleClient.SetDefaultMethodHandlerAsync(testMethodCallback);
            Assert.True(moduleClient.IsActive);

            Assert.True(moduleClient.IsActive);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.False(moduleClient.IsActive);
            sdkModuleClient.Verify(s => s.CloseAsync(), Times.Once);
        }

        [Theory]
        [InlineData(typeof(ObjectDisposedException))]
        [InlineData(typeof(NullReferenceException))]
        public async Task HandleExceptionsTest(Type exception)
        {
            // Arrange
            MethodCallback testMethodCallback = (request, context) => Task.FromResult(new MethodResponse(200));

            var sdkModuleClient = new Mock<ISdkModuleClient>(MockBehavior.Strict);
            sdkModuleClient.Setup(s => s.CloseAsync())
                .Returns(Task.CompletedTask);
            sdkModuleClient.Setup(s => s.SetDefaultMethodHandlerAsync(testMethodCallback))
                .ThrowsAsync((Exception)Activator.CreateInstance(exception, "Dummy exception"));

            TimeSpan idleTimeout = TimeSpan.FromMinutes(3);
            bool closeOnIdleTimeout = false;

            // Act
            var moduleClient = new ModuleClient(sdkModuleClient.Object, idleTimeout, closeOnIdleTimeout, Core.UpstreamProtocol.Mqtt);
            Assert.Equal(Core.UpstreamProtocol.Mqtt, moduleClient.UpstreamProtocol);
            Assert.True(moduleClient.IsActive);
            await Assert.ThrowsAsync(exception, () => moduleClient.SetDefaultMethodHandlerAsync(testMethodCallback));

            // Assert
            sdkModuleClient.Verify(s => s.SetDefaultMethodHandlerAsync(testMethodCallback), Times.Once);
            sdkModuleClient.Verify(s => s.CloseAsync(), Times.Once);
        }
    }
}
