// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class RestartRequestHandlerTest
    {
        [Fact]
        public async Task RestartTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var moduleRuntimeInfo1 = new ModuleRuntimeInfo("mod1", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var moduleRuntimeInfo2 = new ModuleRuntimeInfo("mod2", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>(MockBehavior.Strict);
            runtimeInfoProvider.Setup(r => r.GetModules(cts.Token))
                .ReturnsAsync(new[] { moduleRuntimeInfo1, moduleRuntimeInfo2 });

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync("mod1")).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(runtimeInfoProvider.Object, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod1\"}";
            
            // Act
            Option<string> response = await restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token);

            // Assert
            Assert.False(response.HasValue);
            runtimeInfoProvider.Verify(r => r.GetModules(cts.Token), Times.Once);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Once);
            commandFactory.Verify(c => c.RestartAsync("mod1"), Times.Once);
        }

        [Fact]
        public async Task InvalidModuleTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var moduleRuntimeInfo1 = new ModuleRuntimeInfo("mod1", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var moduleRuntimeInfo2 = new ModuleRuntimeInfo("mod2", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>(MockBehavior.Strict);
            runtimeInfoProvider.Setup(r => r.GetModules(cts.Token))
                .ReturnsAsync(new[] { moduleRuntimeInfo1, moduleRuntimeInfo2 });

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync("mod1")).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(runtimeInfoProvider.Object, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod3\"}";

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            //// Assert
            runtimeInfoProvider.Verify(r => r.GetModules(cts.Token), Times.Once);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync("mod1"), Times.Never);
        }

        [Fact]
        public async Task InvalidSchemaVersionTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var moduleRuntimeInfo1 = new ModuleRuntimeInfo("mod1", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var moduleRuntimeInfo2 = new ModuleRuntimeInfo("mod2", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>(MockBehavior.Strict);
            runtimeInfoProvider.Setup(r => r.GetModules(cts.Token))
                .ReturnsAsync(new[] { moduleRuntimeInfo1, moduleRuntimeInfo2 });

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync("mod1")).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(runtimeInfoProvider.Object, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"2.0\",\"id\": \"mod1\"}";

            // Act
            await Assert.ThrowsAsync<InvalidSchemaVersionException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            //// Assert
            runtimeInfoProvider.Verify(r => r.GetModules(cts.Token), Times.Never);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync("mod1"), Times.Never);
        }

        [Fact]
        public async Task InvalidModuleStateTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var moduleRuntimeInfo1 = new ModuleRuntimeInfo("mod1", "docker", ModuleStatus.Backoff, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var moduleRuntimeInfo2 = new ModuleRuntimeInfo("mod2", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>(MockBehavior.Strict);
            runtimeInfoProvider.Setup(r => r.GetModules(cts.Token))
                .ReturnsAsync(new[] { moduleRuntimeInfo1, moduleRuntimeInfo2 });

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync("mod1")).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(runtimeInfoProvider.Object, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod1\"}";

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            //// Assert
            runtimeInfoProvider.Verify(r => r.GetModules(cts.Token), Times.Once);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync("mod1"), Times.Never);
        }

        [Theory]
        [InlineData("{\"schemaVersion\": \"1.0\",\"id2\": \"mod1\"}")]
        [InlineData("")]
        public async Task InvalidPayloadTest(string payload)
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var moduleRuntimeInfo1 = new ModuleRuntimeInfo("mod1", "docker", ModuleStatus.Backoff, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var moduleRuntimeInfo2 = new ModuleRuntimeInfo("mod2", "docker", ModuleStatus.Running, "", 0, Util.Option.None<DateTime>(), Util.Option.None<DateTime>());
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>(MockBehavior.Strict);
            runtimeInfoProvider.Setup(r => r.GetModules(cts.Token))
                .ReturnsAsync(new[] { moduleRuntimeInfo1, moduleRuntimeInfo2 });

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync("mod1")).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(runtimeInfoProvider.Object, commandFactory.Object);

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            //// Assert
            runtimeInfoProvider.Verify(r => r.GetModules(cts.Token), Times.Never);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync("mod1"), Times.Never);
        }
    }
}
