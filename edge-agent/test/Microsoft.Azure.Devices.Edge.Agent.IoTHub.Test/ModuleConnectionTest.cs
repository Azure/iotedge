// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModuleConnectionTest
    {
        [Fact]
        public async Task CreateAndInitTest()
        {
            // Arrange
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (status, reason) => { };
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = (properties, context) => Task.CompletedTask;

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.IsActive).Returns(true);
            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(connectionStatusChangesHandler))
                .ReturnsAsync(moduleClient.Object);

            var requestManager = new Mock<IRequestManager>();
            bool enableSubscriptions = true;

            // Act
            var moduleConnection = new ModuleConnection(moduleClientProvider.Object, requestManager.Object, connectionStatusChangesHandler, desiredPropertyUpdateCallback, enableSubscriptions);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Option<IModuleClient> resultModuleClientOption = moduleConnection.GetModuleClient();

            // Assert
            Assert.True(resultModuleClientOption.HasValue);
            Assert.Equal(moduleClient.Object, resultModuleClientOption.OrDefault());
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Once);
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);

            // Act
            IModuleClient resultModuleClient = await moduleConnection.GetOrCreateModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            Assert.Equal(moduleClient.Object, resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Once);
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);
        }

        [Fact]
        public async Task CreateAndCloseTest()
        {
            // Arrange
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (status, reason) => { };
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = (properties, context) => Task.CompletedTask;

            Task<IModuleClient> GetModuleClient() => Task.FromResult(Mock.Of<IModuleClient>(m => m.IsActive));
            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(connectionStatusChangesHandler))
                .Returns(GetModuleClient);

            var requestManager = new Mock<IRequestManager>();
            bool enableSubscriptions = true;

            // Act
            var moduleConnection = new ModuleConnection(moduleClientProvider.Object, requestManager.Object, connectionStatusChangesHandler, desiredPropertyUpdateCallback, enableSubscriptions);
            IModuleClient resultModuleClient = await moduleConnection.GetOrCreateModuleClient();
            Option<IModuleClient> optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            Assert.True(optionResultModuleClient.HasValue);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Once);
            Mock<IModuleClient> moduleClient = Mock.Get(resultModuleClient);
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);

            // Act - Set the client to not active and try to get a Get a module client
            moduleClient.Setup(m => m.IsActive).Returns(false);
            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.False(optionResultModuleClient.HasValue);

            // Act
            resultModuleClient = await moduleConnection.GetOrCreateModuleClient();
            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            Assert.True(optionResultModuleClient.HasValue);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(2));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);

            // Act - Set the client to not active and raise the client closed event
            moduleClient.Setup(m => m.IsActive).Returns(false);
            moduleClient.Raise(m => m.Closed += null, new EventArgs());

            await Task.Delay(TimeSpan.FromSeconds(3));

            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.True(optionResultModuleClient.HasValue);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(3));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);

            // Act
            resultModuleClient = await moduleConnection.GetOrCreateModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(3));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Once);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Once);
        }

        [Fact]
        public async Task SubscriptionsDisabledTest()
        {
            // Arrange
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (status, reason) => { };
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = (properties, context) => Task.CompletedTask;

            Task<IModuleClient> GetModuleClient() => Task.FromResult(Mock.Of<IModuleClient>(m => m.IsActive));
            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(connectionStatusChangesHandler))
                .Returns(GetModuleClient);

            var requestManager = new Mock<IRequestManager>();
            bool enableSubscriptions = false;

            // Act
            var moduleConnection = new ModuleConnection(moduleClientProvider.Object, requestManager.Object, connectionStatusChangesHandler, desiredPropertyUpdateCallback, enableSubscriptions);
            await Task.Delay(TimeSpan.FromSeconds(5));

            IModuleClient resultModuleClient = await moduleConnection.GetOrCreateModuleClient();
            Option<IModuleClient> optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            Assert.True(optionResultModuleClient.HasValue);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Once);
            Mock<IModuleClient> moduleClient = Mock.Get(resultModuleClient);
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Never);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Never);

            // Act - Set the client to not active and try to get a Get a module client
            moduleClient.Setup(m => m.IsActive).Returns(false);
            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.False(optionResultModuleClient.HasValue);

            // Act
            resultModuleClient = await moduleConnection.GetOrCreateModuleClient();
            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            Assert.True(optionResultModuleClient.HasValue);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(2));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Never);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Never);

            // Act - Set the client to not active and raise the client closed event
            moduleClient.Setup(m => m.IsActive).Returns(false);
            moduleClient.Raise(m => m.Closed += null, new EventArgs());

            // Wait for some time. The ModuleClient should not get automatically reinitialized since subscriptions are disabled.
            await Task.Delay(TimeSpan.FromSeconds(5));

            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.False(optionResultModuleClient.HasValue);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(2));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Never);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Never);

            // Act
            resultModuleClient = await moduleConnection.GetOrCreateModuleClient();

            // Assert
            Assert.NotNull(resultModuleClient);
            moduleClient = Mock.Get(resultModuleClient);
            moduleClientProvider.Verify(m => m.Create(connectionStatusChangesHandler), Times.Exactly(3));
            moduleClient.Verify(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>()), Times.Never);
            moduleClient.Verify(m => m.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback), Times.Never);

            // Act
            optionResultModuleClient = moduleConnection.GetModuleClient();

            // Assert
            Assert.True(optionResultModuleClient.HasValue);
        }

        [Fact]
        public async Task FailingInitClosesModuleClient()
        {
            // Arrange
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (status, reason) => { };
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = (properties, context) => Task.CompletedTask;

            var milestone = new SemaphoreSlim(0, 1);

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.SetDefaultMethodHandlerAsync(It.IsAny<MethodCallback>())).Callback(() => milestone.Release()).Throws<TimeoutException>();

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(connectionStatusChangesHandler)).ReturnsAsync(moduleClient.Object);

            var requestManager = new Mock<IRequestManager>();
            bool enableSubscriptions = true;

            // Act
            var moduleConnection = new ModuleConnection(moduleClientProvider.Object, requestManager.Object, connectionStatusChangesHandler, desiredPropertyUpdateCallback, enableSubscriptions);
            await milestone.WaitAsync();
            await Task.Delay(TimeSpan.FromSeconds(0.5)); // the milestone is released a bit earlier than the exception, so wait a tiny bit

            // Assert
            moduleClient.Verify(m => m.CloseAsync(), Times.Once);
        }
    }
}
