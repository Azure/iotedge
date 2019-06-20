// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class RestartPlannerTest
    {
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IRuntimeInfo RuntimeInfo = Mock.Of<IRuntimeInfo>();

        [Fact]
        [Unit]
        public async void RestartPlannerMinimalTest()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            var addExecutionList = new List<TestRecordType>();
            Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, ModuleSet.Empty, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAdd1RunningModule()
        {
            var factory = new TestCommandFactory();

            IModule addModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);

            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { addModule });

            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            ModuleSet addRunning = ModuleSet.Create(addModule);
            var addExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, addModule),
                new TestRecordType(TestCommandType.TestStart, addModule),
            };
            Plan addPlan = await planner.PlanAsync(addRunning, ModuleSet.Empty, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAdd1StoppedModule()
        {
            var factory = new TestCommandFactory();

            IModule stoppedModule = new TestModule("mod1", "version1", "test", ModuleStatus.Stopped, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            ModuleSet addStopped = ModuleSet.Create(stoppedModule);

            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { stoppedModule });
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            var addStoppedExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, stoppedModule),
            };
            Plan addStoppedPlan = await planner.PlanAsync(addStopped, ModuleSet.Empty, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addStoppedPlan, token);

            factory.Recorder.ForEach(r => Assert.Equal(addStoppedExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerUpdate1Module()
        {
            var factory = new TestCommandFactory();

            IModule currentModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            IModule desiredModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);

            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(new List<IModule>() { desiredModule });
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            ModuleSet currentSet = ModuleSet.Create(currentModule);
            ModuleSet desiredSet = ModuleSet.Create(desiredModule);
            var updateExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, currentModule),
                new TestRecordType(TestCommandType.TestUpdate, desiredModule),
                new TestRecordType(TestCommandType.TestStart, desiredModule),
            };
            Plan addPlan = await planner.PlanAsync(desiredSet, currentSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            factory.Recorder.ForEach(r => Assert.Equal(updateExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerRemove1Module()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            IModule removeModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            ModuleSet removeRunning = ModuleSet.Create(removeModule);
            var removeExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, removeModule),
                new TestRecordType(TestCommandType.TestRemove, removeModule),
            };
            Plan addPlan = await planner.PlanAsync(ModuleSet.Empty, removeRunning, RuntimeInfo, ImmutableDictionary<string, IModuleIdentity>.Empty);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            factory.Recorder.ForEach(r => Assert.Equal(removeExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAddRemoveUpdate()
        {
            var factory = new TestCommandFactory();
            var token = new CancellationToken();
            DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);
            DateTime lastExitTime = lastStartTime.AddDays(1);

            var currentModules = new List<IModule>
            {
                new TestRuntimeModule("UpdateMod1", "version1", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running, Config1, 0, "Running", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running),
                new TestRuntimeModule("UpdateMod2", "version1", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running, Config1, 0, "Running", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Stopped),
                new TestRuntimeModule("RemoveMod1", "version1", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running, Config1, 0, "Running", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Running),
                new TestRuntimeModule("RemoveMod2", "version1", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running, Config1, 0, "Running", lastStartTime, lastExitTime, 0, DateTime.MinValue, ModuleStatus.Stopped)
            };
            var desiredModules = new List<IModule>
            {
                new TestModule("NewMod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars),
                new TestModule("NewMod2", "version1", "test", ModuleStatus.Stopped, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars),
                new TestModule("UpdateMod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars),
                new TestModule("UpdateMod2", "version1", "test", ModuleStatus.Stopped, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)
            };

            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = GetModuleIdentities(desiredModules);
            var planner = new RestartPlanner(factory);

            ModuleSet currentSet = ModuleSet.Create(currentModules.ToArray());
            ModuleSet desiredSet = ModuleSet.Create(desiredModules.ToArray());
            var updateExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, currentModules[0]),
                new TestRecordType(TestCommandType.TestStop, currentModules[1]),
                new TestRecordType(TestCommandType.TestStop, currentModules[2]),
                new TestRecordType(TestCommandType.TestStop, currentModules[3]),
                new TestRecordType(TestCommandType.TestRemove, currentModules[2]),
                new TestRecordType(TestCommandType.TestRemove, currentModules[3]),
                new TestRecordType(TestCommandType.TestCreate, desiredModules[0]),
                new TestRecordType(TestCommandType.TestCreate, desiredModules[1]),
                new TestRecordType(TestCommandType.TestStart, desiredModules[0]),
                new TestRecordType(TestCommandType.TestStart, desiredModules[2]),
            };
            Plan addPlan = await planner.PlanAsync(desiredSet, currentSet, RuntimeInfo, moduleIdentities);
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, addPlan, token);

            // Weak confirmation: no assumed order.
            factory.Recorder.ForEach(recorder => Assert.All(updateExecutionList, r => Assert.Contains(r, recorder.ExecutionList)));
            factory.Recorder.ForEach(
                recorder =>
                {
                    // One way to validate order
                    // UpdateMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[0])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[8])));
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[9])));
                    // UpdateMod2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[1])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[9])));
                    // RemoveMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[3])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[5])));
                    // RemoveMod2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[4])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])));
                    // AddMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[8])));
                    // AddModTrue2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[0])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])));
                });
        }

        static IImmutableDictionary<string, IModuleIdentity> GetModuleIdentities(IList<IModule> modules)
        {
            var credential = new ConnectionStringCredentials("fake");
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
