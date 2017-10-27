// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class AgentTests
    {
        [Fact]
        [Unit]
        public void AgentConstructorInvalidArgs()
        {
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            Assert.Throws<ArgumentNullException>(() => new Agent(null, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleLifecycleManager.Object));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, null, mockPlanner.Object, mockReporter.Object, mockModuleLifecycleManager.Object));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironment.Object, null, mockReporter.Object, mockModuleLifecycleManager.Object));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, null, mockModuleLifecycleManager.Object));
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncOnEmptyPlan()
        {
            var token = new CancellationToken();

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>
            {
                { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, new ConfigurationInfo("1")) }
            });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            ModuleSet currentModuleSet = desiredModuleSet;

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentModuleSet);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(Plan.Empty));

            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Once);
            mockPlanner.Verify(pl => pl.PlanAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
            mockReporter.Verify(r => r.ReportAsync(token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success), Times.Once);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenConfigSourceThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var currentSet = ModuleSet.Empty;
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).Throws<InvalidOperationException>();
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockReporter.Setup(r => r.ReportAsync(token, currentSet, null, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            // Act
            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenConfigSourceReturnsConfigFormatException()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var currentSet = ModuleSet.Empty;
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            var deploymentConfigInfo = new DeploymentConfigInfo(10, new ConfigFormatException("Bad config"));
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockReporter.Setup(r => r.ReportAsync(token, currentSet, deploymentConfigInfo, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.ConfigFormatError)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            // Act
            // Assert
            await Assert.ThrowsAsync<ConfigFormatException>(() => agent.ReconcileAsync(token));
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenEnvironmentSourceThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>());
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token)).Throws<InvalidOperationException>();
            mockReporter.Setup(r => r.ReportAsync(token, null, deploymentConfigInfo, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            // Act
            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenModuleIdentityLifecycleManagerThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>
            {
                { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, new ConfigurationInfo("1")) }
            });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(ModuleSet.Empty);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), ModuleSet.Empty))
                .Throws<InvalidOperationException>();
            mockReporter.Setup(r => r.ReportAsync(token, ModuleSet.Empty, deploymentConfigInfo, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            // Act
            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncOnSetPlan()
        {
            var desiredModule = new TestModule("desired", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, new ConfigurationInfo("1"));
            var currentModule = new TestModule("current", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, new ConfigurationInfo("1"));
            Option<TestPlanRecorder> recordKeeper = Option.Some(new TestPlanRecorder());
            var moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, desiredModule),
                new TestRecordType(TestCommandType.TestRemove, currentModule)

            };
            var commandList = new List<ICommand>
            {
                new TestCommand(TestCommandType.TestCreate, desiredModule, recordKeeper),
                new TestCommand(TestCommandType.TestRemove, currentModule, recordKeeper)
            };
            var testPlan = new Plan(commandList);

            var token = new CancellationToken();

            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>() { ["desired"] = desiredModule });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredSet = deploymentConfig.GetModuleSet();
            ModuleSet currentSet = ModuleSet.Create(currentModule);

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(desiredSet, currentSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(testPlan));
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), currentSet))
                .Returns(Task.FromResult((IImmutableDictionary<String, IModuleIdentity>)ImmutableDictionary<String, IModuleIdentity>.Empty));
            mockReporter.Setup(r => r.ReportAsync(token, currentSet, deploymentConfigInfo, DeploymentStatus.Success))
                .Returns(Task.CompletedTask);

            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Exactly(2));
            mockPlanner.Verify(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
            mockReporter.VerifyAll();
            recordKeeper.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async Task DesiredIsNotNullBecauseCurrentThrew()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var runtimeInfo = new Mock<IRuntimeInfo>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var token = new CancellationToken();

            var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", runtimeInfo.Object,  new SystemModules(null, null), ImmutableDictionary<string, IModule>.Empty));
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(e => e.GetModulesAsync(token))
                .Throws<InvalidOperationException>();

            // Act
            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));

            // Assert
            mockReporter.Verify(r => r.ReportAsync(token, null, deploymentConfigInfo, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.Failed)));
        }

        [Fact]
        [Unit]
        public async Task CurrentIsNotNullBecauseDesiredThrew()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var runtimeInfo = new Mock<IRuntimeInfo>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var token = new CancellationToken();

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .Throws<InvalidOperationException>();
            mockEnvironment.Setup(e => e.GetModulesAsync(token))
                .ReturnsAsync(ModuleSet.Empty);

            // Act
            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));

            // Assert
            mockReporter.Verify(r => r.ReportAsync(token, ModuleSet.Empty, null, It.Is<DeploymentStatus>(s => s.Code == DeploymentStatusCode.Failed)));
        }
    }
}
