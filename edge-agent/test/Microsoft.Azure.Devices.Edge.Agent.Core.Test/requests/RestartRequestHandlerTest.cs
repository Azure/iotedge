// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Collections.Generic;
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
            var edgeAgent = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHub = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var mod1 = Mock.Of<IRuntimeModule>(m => m.Name == "mod1" && m.RuntimeStatus == ModuleStatus.Running);
            var mod2 = Mock.Of<IRuntimeModule>(m => m.Name == "mod2" && m.RuntimeStatus == ModuleStatus.Running);
            var deploymentConfigInfo = new DeploymentConfigInfo(
                1,
                new DeploymentConfig(
                    "1.0",
                    Mock.Of<IRuntimeInfo>(),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>
                    {
                        ["mod1"] = mod1,
                        ["mod2"] = mod2
                    }));
            var configSource = Mock.Of<IConfigSource>(c => c.GetDeploymentConfigInfoAsync() == Task.FromResult(deploymentConfigInfo));

            var moduleSet = ModuleSet.Create(edgeAgent, edgeHub, mod1, mod2);
            var environment = Mock.Of<IEnvironment>(e => e.GetModulesAsync(cts.Token) == Task.FromResult(moduleSet));
            var environmentProvider = Mock.Of<IEnvironmentProvider>(e => e.Create(deploymentConfigInfo.DeploymentConfig) == environment);

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync(mod1)).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(environmentProvider, configSource, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod1\"}";

            // Act
            Option<string> response = await restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token);

            // Assert
            Assert.False(response.HasValue);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Once);
            commandFactory.Verify(c => c.RestartAsync(mod1), Times.Once);
            Mock.Get(configSource).VerifyAll();
            Mock.Get(environmentProvider).VerifyAll();
            Mock.Get(environment).VerifyAll();
        }

        [Fact]
        public async Task InvalidModuleTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var edgeAgent = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHub = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var mod1 = Mock.Of<IRuntimeModule>(m => m.Name == "mod1" && m.RuntimeStatus == ModuleStatus.Running);
            var mod2 = Mock.Of<IRuntimeModule>(m => m.Name == "mod2" && m.RuntimeStatus == ModuleStatus.Running);
            var deploymentConfigInfo = new DeploymentConfigInfo(
                1,
                new DeploymentConfig(
                    "1.0",
                    Mock.Of<IRuntimeInfo>(),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>
                    {
                        ["mod1"] = mod1,
                        ["mod2"] = mod2
                    }));
            var configSource = Mock.Of<IConfigSource>(c => c.GetDeploymentConfigInfoAsync() == Task.FromResult(deploymentConfigInfo));

            var moduleSet = ModuleSet.Create(edgeAgent, edgeHub, mod1, mod2);
            var environment = Mock.Of<IEnvironment>(e => e.GetModulesAsync(cts.Token) == Task.FromResult(moduleSet));
            var environmentProvider = Mock.Of<IEnvironmentProvider>(e => e.Create(deploymentConfigInfo.DeploymentConfig) == environment);

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync(mod1)).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(environmentProvider, configSource, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod3\"}";

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            // Assert
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync(mod1), Times.Never);
            Mock.Get(configSource).VerifyAll();
            Mock.Get(environmentProvider).VerifyAll();
            Mock.Get(environment).VerifyAll();
        }

        [Fact]
        public async Task InvalidSchemaVersionTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var edgeAgent = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHub = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var mod1 = Mock.Of<IRuntimeModule>(m => m.Name == "mod1" && m.RuntimeStatus == ModuleStatus.Running);
            var mod2 = Mock.Of<IRuntimeModule>(m => m.Name == "mod2" && m.RuntimeStatus == ModuleStatus.Running);
            var deploymentConfigInfo = new DeploymentConfigInfo(
                1,
                new DeploymentConfig(
                    "1.0",
                    Mock.Of<IRuntimeInfo>(),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>
                    {
                        ["mod1"] = mod1,
                        ["mod2"] = mod2
                    }));
            var configSource = Mock.Of<IConfigSource>(c => c.GetDeploymentConfigInfoAsync() == Task.FromResult(deploymentConfigInfo));

            var moduleSet = ModuleSet.Create(edgeAgent, edgeHub, mod1, mod2);
            var environment = Mock.Of<IEnvironment>(e => e.GetModulesAsync(cts.Token) == Task.FromResult(moduleSet));
            var environmentProvider = Mock.Of<IEnvironmentProvider>(e => e.Create(deploymentConfigInfo.DeploymentConfig) == environment);

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync(mod1)).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(environmentProvider, configSource, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"2.0\",\"id\": \"mod1\"}";

            // Act
            await Assert.ThrowsAsync<InvalidSchemaVersionException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            // Assert
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync(mod1), Times.Never);
        }

        [Fact]
        public async Task InvalidModuleStateTest()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var edgeAgent = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHub = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var mod1 = Mock.Of<IRuntimeModule>(m => m.Name == "mod1" && m.RuntimeStatus == ModuleStatus.Failed);
            var mod2 = Mock.Of<IRuntimeModule>(m => m.Name == "mod2" && m.RuntimeStatus == ModuleStatus.Running);
            var deploymentConfigInfo = new DeploymentConfigInfo(
                1,
                new DeploymentConfig(
                    "1.0",
                    Mock.Of<IRuntimeInfo>(),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>
                    {
                        ["mod1"] = mod1,
                        ["mod2"] = mod2
                    }));
            var configSource = Mock.Of<IConfigSource>(c => c.GetDeploymentConfigInfoAsync() == Task.FromResult(deploymentConfigInfo));

            var moduleSet = ModuleSet.Create(edgeAgent, edgeHub, mod1, mod2);
            var environment = Mock.Of<IEnvironment>(e => e.GetModulesAsync(cts.Token) == Task.FromResult(moduleSet));
            var environmentProvider = Mock.Of<IEnvironmentProvider>(e => e.Create(deploymentConfigInfo.DeploymentConfig) == environment);

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync(mod1)).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(environmentProvider, configSource, commandFactory.Object);

            string payload = "{\"schemaVersion\": \"1.0\",\"id\": \"mod1\"}";

            // Act
            Option<string> response = await restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token);

            // Assert
            Assert.False(response.HasValue);
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Once);
            commandFactory.Verify(c => c.RestartAsync(mod1), Times.Once);
            Mock.Get(configSource).VerifyAll();
            Mock.Get(environmentProvider).VerifyAll();
            Mock.Get(environment).VerifyAll();
        }

        [Theory]
        [InlineData("{\"schemaVersion\": \"1.0\",\"id2\": \"mod1\"}")]
        [InlineData("")]
        public async Task InvalidPayloadTest(string payload)
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var edgeAgent = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHub = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var mod1 = Mock.Of<IRuntimeModule>(m => m.Name == "mod1" && m.RuntimeStatus == ModuleStatus.Running);
            var mod2 = Mock.Of<IRuntimeModule>(m => m.Name == "mod2" && m.RuntimeStatus == ModuleStatus.Running);
            var deploymentConfigInfo = new DeploymentConfigInfo(
                1,
                new DeploymentConfig(
                    "1.0",
                    Mock.Of<IRuntimeInfo>(),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>
                    {
                        ["mod1"] = mod1,
                        ["mod2"] = mod2
                    }));
            var configSource = Mock.Of<IConfigSource>(c => c.GetDeploymentConfigInfoAsync() == Task.FromResult(deploymentConfigInfo));

            var moduleSet = ModuleSet.Create(edgeAgent, edgeHub, mod1, mod2);
            var environment = Mock.Of<IEnvironment>(e => e.GetModulesAsync(cts.Token) == Task.FromResult(moduleSet));
            var environmentProvider = Mock.Of<IEnvironmentProvider>(e => e.Create(deploymentConfigInfo.DeploymentConfig) == environment);

            var restartCommand = new Mock<ICommand>(MockBehavior.Strict);
            restartCommand.Setup(r => r.ExecuteAsync(cts.Token))
                .Returns(Task.CompletedTask);
            var commandFactory = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactory.Setup(c => c.RestartAsync(mod1)).ReturnsAsync(restartCommand.Object);

            var restartRequestHandler = new RestartRequestHandler(environmentProvider, configSource, commandFactory.Object);

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => restartRequestHandler.HandleRequest(Option.Some(payload), cts.Token));

            // Assert
            restartCommand.Verify(r => r.ExecuteAsync(cts.Token), Times.Never);
            commandFactory.Verify(c => c.RestartAsync(mod1), Times.Never);
        }
    }
}
