// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class AgentTests
    {
        public static IEnumerable<object[]> GetExceptionsToTest() => new List<object[]>
        {
            new object[]
            {
                new ConfigEmptyException("Empty config"),
                DeploymentStatusCode.ConfigEmptyError
            },
            new object[]
            {
                new InvalidSchemaVersionException("Bad schema"),
                DeploymentStatusCode.InvalidSchemaVersion
            },
            new object[]
            {
                new ConfigFormatException("Bad config"),
                DeploymentStatusCode.ConfigFormatError
            }
        };

        [Fact]
        [Unit]
        public void AgentConstructorInvalidArgs()
        {
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironmentProvider = new Mock<IEnvironmentProvider>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();

            Assert.Throws<ArgumentNullException>(() => new Agent(null, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, null, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, null, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, null, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, null, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, null, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, null, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, null, serde, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, null, encryptionDecryptionProvider));
            Assert.Throws<ArgumentNullException>(() => new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, null));
        }

        [Fact]
        [Unit]
        public async void AgentCreateSuccessWhenDecryptFails()
        {
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironmentProvider = new Mock<IEnvironmentProvider>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = new Mock<IEntityStore<string, string>>();
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = new Mock<IEncryptionProvider>();
            configStore.Setup(cs => cs.Get(It.IsAny<string>()))
                .ReturnsAsync(Option.Some("encrypted"));
            encryptionDecryptionProvider.Setup(ep => ep.DecryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));

            Agent agent = await Agent.Create(mockConfigSource.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleLifecycleManager.Object, mockEnvironmentProvider.Object, configStore.Object, serde, encryptionDecryptionProvider.Object);

            Assert.NotNull(agent);
            encryptionDecryptionProvider.Verify(ep => ep.DecryptAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncOnEmptyPlan()
        {
            var token = new CancellationToken();
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockEnvironmentProvider = new Mock<IEnvironmentProvider>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(null, null),
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null) }
                });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            ModuleSet currentModuleSet = desiredModuleSet;

            mockEnvironmentProvider.Setup(m => m.Create(It.IsAny<DeploymentConfig>())).Returns(mockEnvironment.Object);
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentModuleSet);
            mockEnvironment.Setup(env => env.GetRuntimeInfoAsync()).ReturnsAsync(runtimeInfo);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet, runtimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(Plan.Empty));

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Once);
            mockPlanner.Verify(pl => pl.PlanAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet, runtimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
            mockReporter.Verify(r => r.ReportAsync(token, currentModuleSet, runtimeInfo, DeploymentConfigInfo.Empty.Version, Option.None<DeploymentStatus>()), Times.Once);
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, Plan.Empty, token), Times.Never);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenConfigSourceThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var currentSet = ModuleSet.Empty;
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).Throws<InvalidOperationException>();
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockReporter.Setup(r => r.ReportAsync(token, It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            // Act
            // Assert
            await agent.ReconcileAsync(token);
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetExceptionsToTest))]
        public async void ReconcileAsyncAbortsWhenConfigSourceReturnsKnownExceptions(
            Exception testException,
            DeploymentStatusCode statusCode)
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var currentSet = ModuleSet.Empty;
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfigInfo = new DeploymentConfigInfo(10, testException);
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockReporter.Setup(r => r.ReportAsync(token, It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == statusCode)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            // Act
            // Assert
            await agent.ReconcileAsync(token);
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenEnvironmentSourceThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), new Dictionary<string, IModule>());
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token)).Throws<InvalidOperationException>();
            mockReporter.Setup(r => r.ReportAsync(token, null, It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            // Act
            // Assert
            await agent.ReconcileAsync(token);
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncAbortsWhenModuleIdentityLifecycleManagerThrows()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var token = new CancellationToken();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                Mock.Of<IRuntimeInfo>(),
                new SystemModules(null, null),
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null) }
                });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(ModuleSet.Empty);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), ModuleSet.Empty))
                .Throws<InvalidOperationException>();
            mockReporter.Setup(r => r.ReportAsync(token, It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == DeploymentStatusCode.Failed)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            // Act
            // Assert
            await agent.ReconcileAsync(token);
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Never);
            mockReporter.VerifyAll();
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncReportsFailedWhenEncryptProviderThrows()
        {
            var token = new CancellationToken();
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockEnvironmentProvider = new Mock<IEnvironmentProvider>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var encryptionDecryptionProvider = new Mock<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(null, null),
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null) }
                });
            var desiredModule = new TestModule("desired", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);
            var recordKeeper = Option.Some(new TestPlanRecorder());
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredModuleSet = deploymentConfig.GetModuleSet();
            ModuleSet currentModuleSet = desiredModuleSet;

            var commandList = new List<ICommand>
            {
                new TestCommand(TestCommandType.TestCreate, desiredModule, recordKeeper)
            };
            var testPlan = new Plan(commandList);

            mockEnvironmentProvider.Setup(m => m.Create(It.IsAny<DeploymentConfig>())).Returns(mockEnvironment.Object);
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentModuleSet);
            mockEnvironment.Setup(env => env.GetRuntimeInfoAsync()).ReturnsAsync(runtimeInfo);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.Is<ModuleSet>(ms => ms.Equals(desiredModuleSet)), currentModuleSet, runtimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .ReturnsAsync(testPlan);
            encryptionDecryptionProvider.Setup(ep => ep.EncryptAsync(It.IsAny<string>()))
                .ThrowsAsync(new WorkloadCommunicationException("failed", 404));

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider.Object, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider.Object);

            await agent.ReconcileAsync(token);

            // Assert
            mockPlanner.Verify(p => p.PlanAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<ImmutableDictionary<string, IModuleIdentity>>()), Times.Once);
            mockReporter.Verify(r => r.ReportAsync(It.IsAny<CancellationToken>(), It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), 0, Option.Some(new DeploymentStatus(DeploymentStatusCode.Failed, "failed"))));
            mockPlanRunner.Verify(r => r.ExecuteAsync(0, It.IsAny<Plan>(), token), Times.Once);
            encryptionDecryptionProvider.Verify(ep => ep.EncryptAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        [Unit]
        public async void ReconcileAsyncOnSetPlan()
        {
            var desiredModule = new TestModule("desired", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);
            var currentModule = new TestModule("current", "v1", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);
            var recordKeeper = Option.Some(new TestPlanRecorder());
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

            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var deploymentConfig = new DeploymentConfig("1.0", runtimeInfo, new SystemModules(null, null), new Dictionary<string, IModule> { ["desired"] = desiredModule });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            ModuleSet desiredSet = deploymentConfig.GetModuleSet();
            ModuleSet currentSet = ModuleSet.Create(currentModule);

            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var planRunner = new OrderedPlanRunner();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(env => env.GetModulesAsync(token))
                .ReturnsAsync(currentSet);
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(desiredSet, currentSet))
                .ReturnsAsync(ImmutableDictionary<string, IModuleIdentity>.Empty);
            mockPlanner.Setup(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, runtimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty))
                .Returns(Task.FromResult(testPlan));
            mockModuleIdentityLifecycleManager.Setup(m => m.GetModuleIdentitiesAsync(It.IsAny<ModuleSet>(), currentSet))
                .Returns(Task.FromResult((IImmutableDictionary<string, IModuleIdentity>)ImmutableDictionary<string, IModuleIdentity>.Empty));
            mockReporter.Setup(r => r.ReportAsync(token, It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), Option.Some(DeploymentStatus.Success)))
                .Returns(Task.CompletedTask);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, planRunner, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            await agent.ReconcileAsync(token);

            mockEnvironment.Verify(env => env.GetModulesAsync(token), Times.Exactly(1));
            mockPlanner.Verify(pl => pl.PlanAsync(It.IsAny<ModuleSet>(), currentSet, runtimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty), Times.Once);
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
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var runtimeInfo = new Mock<IRuntimeInfo>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var token = new CancellationToken();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", runtimeInfo.Object, new SystemModules(null, null), ImmutableDictionary<string, IModule>.Empty));
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(e => e.GetModulesAsync(token))
                .Throws<InvalidOperationException>();

            // Act
            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);
            await agent.ReconcileAsync(token);

            // Assert
            mockReporter.Verify(r => r.ReportAsync(token, null, It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == DeploymentStatusCode.Failed)));
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task CurrentIsNotNullBecauseDesiredThrew()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var token = new CancellationToken();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .Throws<InvalidOperationException>();
            mockEnvironment.Setup(e => e.GetModulesAsync(token))
                .ReturnsAsync(ModuleSet.Empty);

            // Act
            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);
            await agent.ReconcileAsync(token);

            // Assert
            mockReporter.Verify(r => r.ReportAsync(token, It.IsAny<ModuleSet>(), It.IsAny<IRuntimeInfo>(), It.IsAny<long>(), It.Is<Option<DeploymentStatus>>(s => s.HasValue && s.OrDefault().Code == DeploymentStatusCode.Failed)));
            mockPlanRunner.Verify(r => r.ExecuteAsync(1, It.IsAny<Plan>(), token), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task ReportShutdownAsyncConfigTest()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();
            var mockEnvironment = new Mock<IEnvironment>();
            var mockPlanner = new Mock<IPlanner>();
            var mockPlanRunner = new Mock<IPlanRunner>();
            var mockReporter = new Mock<IReporter>();
            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                Mock.Of<IRuntimeInfo>(),
                new SystemModules(null, null),
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null) }
                });
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            var token = new CancellationToken();

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            mockEnvironment.Setup(e => e.GetModulesAsync(token))
                .ReturnsAsync(ModuleSet.Empty);

            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);
            await agent.ReportShutdownAsync(token);

            // Assert
            mockReporter.Verify(r => r.ReportShutdown(It.IsAny<DeploymentStatus>(), token));
        }

        [Fact]
        [Unit]
        public async Task HandleShutdownTest()
        {
            // Arrange
            var mockConfigSource = new Mock<IConfigSource>();

            IModule mod1 = new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);
            IModule mod2 = new TestModule("mod2", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), null);
            var modules = new Dictionary<string, IModule>
            {
                [mod1.Name] = mod1,
                [mod2.Name] = mod2
            };

            var mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(m => m.GetModulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModuleSet(modules));

            var mockPlanner = new Mock<IPlanner>();
            mockPlanner.Setup(p => p.CreateShutdownPlanAsync(It.IsAny<ModuleSet>()))
                .ReturnsAsync(new Plan(new ICommand[0]));

            var mockPlanRunner = new Mock<IPlanRunner>();
            mockPlanRunner.Setup(m => m.ExecuteAsync(It.IsAny<long>(), It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
                .Returns(
                    async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        return true;
                    });

            var mockReporter = new Mock<IReporter>();
            mockReporter.Setup(
                    m => m.ReportAsync(
                        It.IsAny<CancellationToken>(),
                        It.IsAny<ModuleSet>(),
                        It.IsAny<IRuntimeInfo>(),
                        It.IsAny<long>(),
                        It.IsAny<Option<DeploymentStatus>>()))
                .Returns(Task.Delay(TimeSpan.FromSeconds(5)));

            var mockModuleIdentityLifecycleManager = new Mock<IModuleIdentityLifecycleManager>();
            var configStore = Mock.Of<IEntityStore<string, string>>();
            var mockEnvironmentProvider = Mock.Of<IEnvironmentProvider>(m => m.Create(It.IsAny<DeploymentConfig>()) == mockEnvironment.Object);
            var serde = Mock.Of<ISerde<DeploymentConfigInfo>>();
            var encryptionDecryptionProvider = Mock.Of<IEncryptionProvider>();
            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), new SystemModules(null, null), modules);
            var deploymentConfigInfo = new DeploymentConfigInfo(0, deploymentConfig);
            var token = new CancellationToken();

            mockConfigSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);

            // Act
            var agent = new Agent(mockConfigSource.Object, mockEnvironmentProvider, mockPlanner.Object, mockPlanRunner.Object, mockReporter.Object, mockModuleIdentityLifecycleManager.Object, configStore, DeploymentConfigInfo.Empty, serde, encryptionDecryptionProvider);

            var shutdownTask = agent.HandleShutdown(token);
            var waitTask = Task.Delay(TimeSpan.FromSeconds(6));
            var completedTask = await Task.WhenAny(shutdownTask, waitTask);

            // Assert
            Assert.Equal(completedTask, shutdownTask);
            mockReporter.Verify(r => r.ReportShutdown(It.IsAny<DeploymentStatus>(), token), Times.Once);
            mockPlanRunner.Verify(r => r.ExecuteAsync(It.IsAny<long>(), It.IsAny<Plan>(), It.IsAny<CancellationToken>()), Times.Once);
            mockPlanner.Verify(r => r.CreateShutdownPlanAsync(It.IsAny<ModuleSet>()), Times.Once);
        }
    }
}
