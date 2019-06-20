// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class HealthRestartPlannerTest
    {
        const int MaxRestartCount = 5;
        const int CoolOffTimeUnitInSeconds = 10;
        static readonly TimeSpan IntensiveCareTime = TimeSpan.FromMinutes(10);
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IRuntimeInfo RuntimeInfo = Mock.Of<IRuntimeInfo>();
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");

        [Fact]
        [Unit]
        public void TestCreateValidation()
        {
            (TestCommandFactory factory, Mock<IEntityStore<string, ModuleState>> store, IRestartPolicyManager restartManager, _) = CreatePlanner();

            Assert.Throws<ArgumentNullException>(() => new HealthRestartPlanner(null, store.Object, IntensiveCareTime, restartManager));
            Assert.Throws<ArgumentNullException>(() => new HealthRestartPlanner(factory, null, IntensiveCareTime, restartManager));
            Assert.Throws<ArgumentNullException>(() => new HealthRestartPlanner(factory, store.Object, IntensiveCareTime, null));
            Assert.NotNull(new HealthRestartPlanner(factory, store.Object, IntensiveCareTime, restartManager));
        }

        [Fact]
        [Unit]
        public async void TestMinimalTest()
        {
            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();
            var token = new CancellationToken();
            var expectedExecutionList = new List<TestRecordType>();

            // Act
            Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, ModuleSet.Empty, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            // Assert
            factory.Recorder.ForEach(r => Assert.Equal(expectedExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestAddRunningModule()
        {
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IModule addModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { addModule });
            ModuleSet addRunning = ModuleSet.Create(addModule);
            var addExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, addModule),
                new TestRecordType(TestCommandType.TestStart, addModule),
            };
            Plan addPlan = await planner.PlanAsync(addRunning, ModuleSet.Empty, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, CancellationToken.None);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestAddStoppedModule()
        {
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IModule addModule = new TestModule("mod1", "version1", "test", ModuleStatus.Stopped, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { addModule });
            ModuleSet addRunning = ModuleSet.Create(addModule);
            var addExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, addModule)
            };
            Plan addPlan = await planner.PlanAsync(addRunning, ModuleSet.Empty, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, CancellationToken.None);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestUpdateModule()
        {
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IRuntimeModule currentModule = new TestRuntimeModule(
                "mod1",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running);
            IModule desiredModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { desiredModule });
            ModuleSet currentSet = ModuleSet.Create(currentModule);
            ModuleSet desiredSet = ModuleSet.Create(desiredModule);

            var updateExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestUpdate, desiredModule),
                new TestRecordType(TestCommandType.TestStart, desiredModule),
            };
            Plan addPlan = await planner.PlanAsync(desiredSet, currentSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, CancellationToken.None);

            factory.Recorder.ForEach(r => Assert.Equal(updateExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestRemoveModule()
        {
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IRuntimeModule removeModule = new TestRuntimeModule(
                "mod1",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running);
            ModuleSet removeRunning = ModuleSet.Create(removeModule);
            var removeExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, removeModule),
                new TestRecordType(TestCommandType.TestRemove, removeModule),
            };
            Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, removeRunning, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, CancellationToken.None);

            factory.Recorder.ForEach(
                r =>
                {
                    Assert.Equal(removeExecutionList, r.ExecutionList);
                    Assert.Single(r.WrappedCommmandList);
                });
        }

        [Fact]
        [Unit]
        public async Task TestRemoveKitchenSink()
        {
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IRuntimeModule[] removedModules = GetRemoveTestData();

            ModuleSet removeRunning = ModuleSet.Create(removedModules.ToArray<IModule>());
            List<TestRecordType> expectedExecutionList = removedModules.SelectMany(
                m => new[]
                {
                    new TestRecordType(TestCommandType.TestStop, m),
                    new TestRecordType(TestCommandType.TestRemove, m)
                }).ToList();
            Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, removeRunning, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, CancellationToken.None);

            factory.Recorder.ForEach(
                r =>
                {
                    Assert.Empty(expectedExecutionList.Except(r.ExecutionList));
                    Assert.Equal(removedModules.Count(), r.WrappedCommmandList.Count);
                });
        }

        [Fact]
        [Unit]
        public async Task TestUpdateDeployKitchenSink()
        {
            // This test makes sure that if a module is being re-deployed due to a
            // configuration change then the runtime state of the module has no impact
            // on whether it undergoes a re-deploy or not.

            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();
            (IRuntimeModule RunningModule, IModule UpdatedModule)[] data = GetUpdateDeployTestData();
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(data.Select(d => d.UpdatedModule).ToList());
            // build "current" and "desired" module sets
            ModuleSet currentModuleSet = ModuleSet.Create(data.Select(d => d.RunningModule).ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(data.Select(d => d.UpdatedModule).ToArray());

            // build expected execution list
            IEnumerable<TestRecordType> expectedExecutionList = data.SelectMany(
                d => new[]
                {
                    new TestRecordType(TestCommandType.TestUpdate, d.UpdatedModule),
                    new TestRecordType(TestCommandType.TestStart, d.UpdatedModule)
                });

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            factory.Recorder.ForEach(
                r =>
                {
                    Assert.Empty(expectedExecutionList.Except(r.ExecutionList));
                    Assert.Equal(data.Length * 2, r.WrappedCommmandList.Count);
                });
        }

        [Fact]
        [Unit]
        public async Task TestUpdateDesiredStateDeployKitchenSink()
        {
            // This test makes sure that if a module is being re-deployed due to a
            // change in the desired status then only the runtime status of the module is changed.

            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();
            (IRuntimeModule RunningModule, IModule UpdatedModule)[] data = GetUpdateDeployStatusChangeTestData();
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(data.Select(d => d.UpdatedModule).ToList());
            // build "current" and "desired" module sets
            ModuleSet currentModuleSet = ModuleSet.Create(data.Select(d => d.RunningModule).ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(data.Select(d => d.UpdatedModule).ToArray());

            // build expected execution list
            IList<TestRecordType> expectedExecutionList = data
                .Where(d => d.UpdatedModule.DesiredStatus != d.RunningModule.RuntimeStatus)
                .Select(
                    d => d.UpdatedModule.DesiredStatus == ModuleStatus.Running
                        ? new TestRecordType(TestCommandType.TestStart, d.RunningModule)
                        : new TestRecordType(TestCommandType.TestStop, d.RunningModule))
                .ToList();

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            Assert.True(factory.Recorder.HasValue);
            factory.Recorder.ForEach(
                r =>
                {
                    Assert.Empty(expectedExecutionList.Except(r.ExecutionList));
                    Assert.Equal(expectedExecutionList.Count, r.ExecutionList.Count);
                    Assert.Equal(expectedExecutionList.Count, r.WrappedCommmandList.Count);
                });
        }

        [Fact]
        [Unit]
        public async Task TestStartStoppedModule()
        {
            // This test makes sure that a module should be running but is in
            // a stopped state is started

            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            (IRuntimeModule RunningModule, IModule UpdatedModule)[] data = GetStoppedModuleTestData();
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(data.Select(d => d.UpdatedModule).ToList());
            // build "current" and "desired" module sets
            ModuleSet currentModuleSet = ModuleSet.Create(data.Select(d => d.RunningModule).ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(data.Select(d => d.UpdatedModule).ToArray());

            // build expected execution list
            IList<TestRecordType> expectedExecutionList = data
                .Where(d => d.UpdatedModule.RestartPolicy > RestartPolicy.Never || d.RunningModule.LastStartTimeUtc == DateTime.MinValue)
                .Select(d => new TestRecordType(TestCommandType.TestStart, d.RunningModule))
                .ToList();

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            Assert.True(factory.Recorder.HasValue);
            factory.Recorder.ForEach(
                r =>
                {
                    Assert.Empty(r.ExecutionList.Except(expectedExecutionList));
                    Assert.Equal(r.ExecutionList.Count, expectedExecutionList.Count);
                });
        }

        [Fact]
        [Unit]
        public async Task TestUpdateStateChangedKitchenSink()
        {
            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            // prepare list of modules whose configurations have been updated
            (IRuntimeModule RunningModule, IModule UpdatedModule)[] updateDeployModules = GetUpdateDeployTestData();

            // prepare list of removed modules
            IEnumerable<IRuntimeModule> removedModules = GetRemoveTestData();

            // prepare a list of existing modules whose runtime status may/may not have been updated
            (IRuntimeModule RunningModule, bool Restart)[] updateStateChangedModules = GetUpdateStateChangeTestData();

            // build "current" and "desired" module sets
            ModuleSet currentModuleSet = ModuleSet.Create(
                updateDeployModules
                    .Select(d => d.RunningModule)
                    .Concat(removedModules)
                    .Concat(updateStateChangedModules.Select(m => m.RunningModule))
                    .ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(
                updateDeployModules
                    .Select(d => d.UpdatedModule)
                    .Concat(updateStateChangedModules.Select(m => m.RunningModule))
                    .ToArray());
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(updateDeployModules.Select(d => d.UpdatedModule).ToList());

            // build expected execution list
            IEnumerable<TestRecordType> expectedExecutionList = updateDeployModules
                .SelectMany(
                    d => new[]
                    {
                        new TestRecordType(TestCommandType.TestUpdate, d.UpdatedModule),
                        new TestRecordType(TestCommandType.TestStart, d.UpdatedModule)
                    })
                .Concat(
                    removedModules.SelectMany(
                        m => new[]
                        {
                            new TestRecordType(TestCommandType.TestStop, m),
                            new TestRecordType(TestCommandType.TestRemove, m)
                        }))
                .Concat(
                    updateStateChangedModules
                        .Where(d => d.Restart)
                        .SelectMany(
                            d => new[]
                            {
                                new TestRecordType(TestCommandType.TestStop, d.RunningModule),
                                new TestRecordType(TestCommandType.TestStart, d.RunningModule)
                            }));

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            factory.Recorder.ForEach(r => Assert.Empty(expectedExecutionList.Except(r.ExecutionList)));
        }

        [Fact]
        [Unit]
        public async Task TestUpdateStateChanged_Offline_NoIdentities()
        {
            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            // prepare list of modules whose configurations have been updated
            (IRuntimeModule RunningModule, IModule UpdatedModule)[] updateDeployModules = GetUpdateDeployTestData();

            // prepare list of removed modules
            IEnumerable<IRuntimeModule> removedModules = GetRemoveTestData();

            // prepare a list of existing modules whose runtime status may/may not have been updated
            (IRuntimeModule RunningModule, bool Restart)[] updateStateChangedModules = GetUpdateStateChangeTestData();

            // build "current" and "desired" module sets
            ModuleSet currentModuleSet = ModuleSet.Create(
                updateDeployModules
                    .Select(d => d.RunningModule)
                    .Concat(removedModules)
                    .Concat(updateStateChangedModules.Select(m => m.RunningModule))
                    .ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(
                updateDeployModules
                    .Select(d => d.UpdatedModule)
                    .Concat(updateStateChangedModules.Select(m => m.RunningModule))
                    .ToArray());
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = ImmutableDictionary<string, IModuleIdentity>.Empty;

            // build expected execution list
            IEnumerable<TestRecordType> expectedExecutionList = removedModules
                .SelectMany(
                    m => new[]
                    {
                        new TestRecordType(TestCommandType.TestStop, m),
                        new TestRecordType(TestCommandType.TestRemove, m)
                    })
                .Concat(
                    updateStateChangedModules
                        .Where(d => d.Restart)
                        .SelectMany(
                            d => new[]
                            {
                                new TestRecordType(TestCommandType.TestStop, d.RunningModule),
                                new TestRecordType(TestCommandType.TestStart, d.RunningModule)
                            }));

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            factory.Recorder.ForEach(r => Assert.Empty(expectedExecutionList.Except(r.ExecutionList)));
        }

        [Fact]
        [Unit]
        public async Task TestResetStatsForHealthyModules()
        {
            // Arrange
            (TestCommandFactory factory, Mock<IEntityStore<string, ModuleState>> store, _, HealthRestartPlanner planner) = CreatePlanner();

            // derive list of "running great" modules from GetUpdateStateChangeTestData()
            IList<IRuntimeModule> runningGreatModules = GetUpdateStateChangeTestData()
                .Where(d => d.Restart == false)
                .Select(d => d.RunningModule)
                .Where(m => m.DesiredStatus == ModuleStatus.Running && m.RuntimeStatus == ModuleStatus.Running)
                .ToList();

            // have the "store" return true when the "Contains" call happens to check if a module has
            // records in the store with stats
            store.Setup(s => s.Contains(It.IsAny<string>()))
                .Returns(() => Task.FromResult(true));

            ModuleSet currentModuleSet = ModuleSet.Create(runningGreatModules.ToArray<IModule>());
            ModuleSet desiredModuleSet = ModuleSet.Create(runningGreatModules.ToArray<IModule>());

            // Act
            Plan plan = await planner.PlanAsync(desiredModuleSet, currentModuleSet, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan, CancellationToken.None);

            // Assert
            factory.Recorder.ForEach(r => Assert.Equal(runningGreatModules.Count(), r.WrappedCommmandList.Count));
        }

        [Unit]
        [Fact]
        public async Task CreateShutdownPlanTest()
        {
            // Arrange
            (TestCommandFactory factory, _, _, HealthRestartPlanner planner) = CreatePlanner();

            IModule module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IModule edgeAgentModule = new TestModule(Constants.EdgeAgentModuleName, "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var modules = new List<IModule>
            {
                module1,
                edgeAgentModule
            };

            ModuleSet running = ModuleSet.Create(modules.ToArray());
            var executionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, module1),
            };

            // Act
            Plan shutdownPlan = await planner.CreateShutdownPlanAsync(running);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, shutdownPlan, CancellationToken.None);

            // Assert
            factory.Recorder.ForEach(r => Assert.Equal(executionList, r.ExecutionList));
        }

        static (TestCommandFactory factory, Mock<IEntityStore<string, ModuleState>> store, IRestartPolicyManager restartManager, HealthRestartPlanner planner) CreatePlanner()
        {
            var factory = new TestCommandFactory();
            var store = new Mock<IEntityStore<string, ModuleState>>();
            var restartManager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            var planner = new HealthRestartPlanner(factory, store.Object, IntensiveCareTime, restartManager);

            return (factory, store, restartManager, planner);
        }

        static IRuntimeModule[] GetRemoveTestData() => new IRuntimeModule[]
        {
            // Always
            new TestRuntimeModule(
                "removeModule1",
                "version1",
                RestartPolicy.Always,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running),
            new TestRuntimeModule(
                "removeModule2",
                "version1",
                RestartPolicy.Always,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Backoff),
            new TestRuntimeModule(
                "removeModule3",
                "version1",
                RestartPolicy.Always,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Unhealthy),
            new TestRuntimeModule(
                "removeModule4",
                "version1",
                RestartPolicy.Always,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Stopped),
            new TestRuntimeModule(
                "removeModule5",
                "version1",
                RestartPolicy.Always,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Failed),

            // OnUnhealthy
            new TestRuntimeModule(
                "removeModule6",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running),
            new TestRuntimeModule(
                "removeModule7",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Backoff),
            new TestRuntimeModule(
                "removeModule8",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Unhealthy),
            new TestRuntimeModule(
                "removeModule9",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Stopped),
            new TestRuntimeModule(
                "removeModule10",
                "version1",
                RestartPolicy.OnUnhealthy,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Failed),

            // OnFailure
            new TestRuntimeModule(
                "removeModule11",
                "version1",
                RestartPolicy.OnFailure,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running),
            new TestRuntimeModule(
                "removeModule12",
                "version1",
                RestartPolicy.OnFailure,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Backoff),
            new TestRuntimeModule(
                "removeModule13",
                "version1",
                RestartPolicy.OnFailure,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Unhealthy),
            new TestRuntimeModule(
                "removeModule14",
                "version1",
                RestartPolicy.OnFailure,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Stopped),
            new TestRuntimeModule(
                "removeModule15",
                "version1",
                RestartPolicy.OnFailure,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Failed),

            // Never
            new TestRuntimeModule(
                "removeModule16",
                "version1",
                RestartPolicy.Never,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running),
            new TestRuntimeModule(
                "removeModule17",
                "version1",
                RestartPolicy.Never,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Backoff),
            new TestRuntimeModule(
                "removeModule18",
                "version1",
                RestartPolicy.Never,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Unhealthy),
            new TestRuntimeModule(
                "removeModule19",
                "version1",
                RestartPolicy.Never,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Stopped),
            new TestRuntimeModule(
                "removeModule20",
                "version1",
                RestartPolicy.Never,
                "test",
                ModuleStatus.Running,
                Config1,
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Failed)
        };

        static (IRuntimeModule RunningModule, IModule UpdatedModule)[] GetUpdateDeployTestData() => new (IRuntimeModule RunningModule, IModule UpdatedModule)[]
        {
            // Always
            (
                new TestRuntimeModule(
                    "updateDeployModule1",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule1", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule2",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule2", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule3",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule3", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule4",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule4", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule5",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule5", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Always, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),

            // OnUnhealthy
            (
                new TestRuntimeModule(
                    "updateDeployModule6",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule6", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule7",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule7", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule8",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule8", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule9",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule9", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule10",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule10", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),

            // OnFailure
            (
                new TestRuntimeModule(
                    "updateDeployModule11",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule11", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnFailure, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule12",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule12", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnFailure, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule13",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule13", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnFailure, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule14",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule14", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnFailure, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule15",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule15", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnFailure, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),

            // Never
            (
                new TestRuntimeModule(
                    "updateDeployModule16",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule16", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Never, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule17",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule17", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Never, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule18",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule18", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Never, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule19",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule19", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Never, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule20",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule("updateDeployModule20", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.Never, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            ),
        };

        static (IRuntimeModule RunningModule, IModule UpdatedModule)[] GetStoppedModuleTestData() => new (IRuntimeModule RunningModule, IModule UpdatedModule)[]
        {
            // Always
            (
                new TestRuntimeModule(
                    "updateDeployModule1",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule1",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // OnUnhealthy
            (
                new TestRuntimeModule(
                    "updateDeployModule2",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule2",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // OnFailure
            (
                new TestRuntimeModule(
                    "updateDeployModule3",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule3",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // Never - Never started
            (
                new TestRuntimeModule(
                    "updateDeployModule4",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule4",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // Never - started before
            (
                new TestRuntimeModule(
                    "updateDeployModule5",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.UtcNow.Subtract(TimeSpan.FromDays(2)),
                    DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule5",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            )
        };

        static (IRuntimeModule RunningModule, IModule UpdatedModule)[] GetUpdateDeployStatusChangeTestData() => new (IRuntimeModule RunningModule, IModule UpdatedModule)[]
        {
            // Always
            (
                new TestRuntimeModule(
                    "updateDeployModule1",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule1",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule2",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule2",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule3",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule3",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule4",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule4",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule5",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule5",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Always,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // OnUnhealthy
            (
                new TestRuntimeModule(
                    "updateDeployModule6",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule6",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule7",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule7",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule8",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule8",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule9",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule9",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule10",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule10",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnUnhealthy,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // OnFailure
            (
                new TestRuntimeModule(
                    "updateDeployModule11",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule11",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule12",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule12",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule13",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule13",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule14",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule14",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule15",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule15",
                    "version1",
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    RestartPolicy.OnFailure,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),

            // Never
            (
                new TestRuntimeModule(
                    "updateDeployModule16",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule16",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule17",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule17",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule18",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule18",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule19",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule19",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
            (
                new TestRuntimeModule(
                    "updateDeployModule20",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed,
                    ImagePullPolicy.OnCreate,
                    null,
                    EnvVars),
                new TestModule(
                    "updateDeployModule20",
                    "version1",
                    "test",
                    ModuleStatus.Stopped,
                    Config1,
                    RestartPolicy.Never,
                    ImagePullPolicy.OnCreate,
                    DefaultConfigurationInfo,
                    EnvVars)
            ),
        };

        static (IRuntimeModule RunningModule, bool Restart)[] GetUpdateStateChangeTestData() => new (IRuntimeModule RunningModule, bool Restart)[]
        {
            ///////////////////////////
            // RestartPolicy.Always
            ///////////////////////////

            // ModuleStatus.Running
            (
                new TestRuntimeModule(
                    "updateStateModule1",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - IntensiveCareTime - TimeSpan.FromMinutes(5),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running),
                false
            ),

            // ModuleStatus.Backoff
            (
                new TestRuntimeModule(
                    "updateStateModule2",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule3",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule4",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),

            // ModuleStatus.Unhealthy
            (
                new TestRuntimeModule(
                    "updateStateModule5",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule6",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule7",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),

            // ModuleStatus.Stopped
            (
                new TestRuntimeModule(
                    "updateStateModule8",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule9",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule10",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),

            // ModuleStatus.Failed
            (
                new TestRuntimeModule(
                    "updateStateModule11",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule12",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule13",
                    "version1",
                    RestartPolicy.Always,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),

            //////////////////////////////
            // RestartPolicy.OnUnhealthy
            //////////////////////////////

            // ModuleStatus.Running
            (
                new TestRuntimeModule(
                    "updateStateModule14",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - IntensiveCareTime - TimeSpan.FromMinutes(5),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running),
                false
            ),

            // ModuleStatus.Backoff
            (
                new TestRuntimeModule(
                    "updateStateModule15",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule16",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule17",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),

            // ModuleStatus.Unhealthy
            (
                new TestRuntimeModule(
                    "updateStateModule18",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule19",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule20",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),

            // ModuleStatus.Stopped
            (
                new TestRuntimeModule(
                    "updateStateModule21",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule22",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule23",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),

            // ModuleStatus.Failed
            (
                new TestRuntimeModule(
                    "updateStateModule24",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule25",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule26",
                    "version1",
                    RestartPolicy.OnUnhealthy,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),

            //////////////////////////////
            // RestartPolicy.OnFailure
            //////////////////////////////

            // ModuleStatus.Running
            (
                new TestRuntimeModule(
                    "updateStateModule27",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - IntensiveCareTime - TimeSpan.FromMinutes(5),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running),
                false
            ),

            // ModuleStatus.Backoff
            (
                new TestRuntimeModule(
                    "updateStateModule28",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule29",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                true
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule30",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),

            // ModuleStatus.Unhealthy
            (
                new TestRuntimeModule(
                    "updateStateModule31",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule32",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule33",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),

            // ModuleStatus.Stopped
            (
                new TestRuntimeModule(
                    "updateStateModule34",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule35",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule36",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),

            // ModuleStatus.Failed
            (
                new TestRuntimeModule(
                    "updateStateModule37",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule38",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule39",
                    "version1",
                    RestartPolicy.OnFailure,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),

            //////////////////////////////
            // RestartPolicy.Never
            //////////////////////////////

            // ModuleStatus.Running
            (
                new TestRuntimeModule(
                    "updateStateModule40",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - IntensiveCareTime - TimeSpan.FromMinutes(5),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running),
                false
            ),

            // ModuleStatus.Backoff
            (
                new TestRuntimeModule(
                    "updateStateModule41",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule42",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule43",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Backoff),
                false
            ),

            // ModuleStatus.Unhealthy
            (
                new TestRuntimeModule(
                    "updateStateModule44",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule45",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule46",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Unhealthy),
                false
            ),

            // ModuleStatus.Stopped
            (
                new TestRuntimeModule(
                    "updateStateModule47",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule48",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule49",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Stopped),
                false
            ),

            // ModuleStatus.Failed
            (
                new TestRuntimeModule(
                    "updateStateModule50",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule51",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromHours(1),
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
            (
                new TestRuntimeModule(
                    "updateStateModule52",
                    "version1",
                    RestartPolicy.Never,
                    "test",
                    ModuleStatus.Running,
                    Config1,
                    0,
                    string.Empty,
                    DateTime.MinValue,
                    DateTime.UtcNow - TimeSpan.FromSeconds(40),
                    3,
                    DateTime.MinValue,
                    ModuleStatus.Failed),
                false
            ),
        };

        static IImmutableDictionary<string, IModuleIdentity> GetModuleIdentities(IList<IModule> modules)
        {
            ICredentials credential = new ConnectionStringCredentials("fake");
            IDictionary<string, IModuleIdentity> identities = new Dictionary<string, IModuleIdentity>();
            foreach (IModule module in modules)
            {
                var identity = new Mock<IModuleIdentity>();
                identity.Setup(id => id.Credentials).Returns(credential);
                identity.Setup(id => id.ModuleId).Returns(module.Name);
                identities.Add(module.Name, identity.Object);
            }

            return identities.ToImmutableDictionary();
        }
    }
}
