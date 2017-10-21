// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using System;
    using System.Collections.Generic;
      using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
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

            ModuleSet desiredSet = ModuleSet.Empty;
            ModuleSet currentSet = ModuleSet.Empty;

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();

            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>());
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
			    .ReturnsAsync(currentSet);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentities(desiredModuleSet, currentSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(desiredSet, currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(Plan.Empty));
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentities(desiredSet, currentSet))
                .Returns(Task.FromResult((IImmutableDictionary<String, IModuleIdentity>)ImmutableDictionary<String, IModuleIdentity>.Empty));

            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Once);
            mockPlanner.Verify(pl => pl.PlanAsync(desiredSet, currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
            mockReporter.Verify(r => r.ReportAsync(token, currentSet, deploymentConfigInfo, new DeploymentStatus(DeploymentStatusCode.Successful)), Times.Once);
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

            var agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            // Act
            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ReconcileAsync(token));
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
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
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentities(desiredSet, currentSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(testPlan));
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentities(It.IsAny<ModuleSet>(), currentSet))
                .Returns(Task.FromResult((IImmutableDictionary<String, IModuleIdentity>)ImmutableDictionary<String, IModuleIdentity>.Empty));
            mockReporter.Setup(r => r.ReportAsync(token, currentSet, deploymentConfigInfo, new DeploymentStatus(DeploymentStatusCode.Successful)))
                .Returns(Task.CompletedTask);

            Agent agent = new Agent(mockConfigSource.Object, mockEnvironment.Object, mockPlanner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Exactly(2));
            mockPlanner.Verify(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
            mockReporter.VerifyAll();
            recordKeeper.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }
    }
}
