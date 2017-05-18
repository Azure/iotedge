// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.planners
{
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class RestartPlannerTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");

        [Fact]
        [Unit]
        public async void RestartPlannerMinimalTest()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            var addExecutionList = new List<TestRecordType>();
            Plan addPlan = planner.Plan(ModuleSet.Empty, ModuleSet.Empty);
            await addPlan.ExecuteAsync(token);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAdd1RunningModule()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            IModule addModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
            ModuleSet addRunning  =ModuleSet.Create(addModule);
            var addExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestPull, addModule),
                new TestRecordType(TestCommandType.TestCreate, addModule),
                new TestRecordType(TestCommandType.TestStart, addModule),
            };
            Plan addPlan = planner.Plan(addRunning, ModuleSet.Empty);
            await addPlan.ExecuteAsync(token);

            factory.Recorder.ForEach(r => Assert.Equal(addExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAdd1StoppedModule()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            IModule stoppedModule = new TestModule("mod1", "version1", "test", ModuleStatus.Stopped, Config2);
            ModuleSet addStopped = ModuleSet.Create(stoppedModule);
            var addStoppedExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestPull, stoppedModule),
                new TestRecordType(TestCommandType.TestCreate, stoppedModule),
            };
            Plan addStoppedPlan = planner.Plan(addStopped, ModuleSet.Empty);
            await addStoppedPlan.ExecuteAsync(token);

            factory.Recorder.ForEach(r => Assert.Equal(addStoppedExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerUpdate1Module()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            IModule currentModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
            IModule desiredModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config2);
            ModuleSet currentSet  =ModuleSet.Create(currentModule);
            ModuleSet desiredSet = ModuleSet.Create(desiredModule);
            var updateExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, currentModule),
                new TestRecordType(TestCommandType.TestPull, desiredModule),
                new TestRecordType(TestCommandType.TestUpdate, desiredModule),
                new TestRecordType(TestCommandType.TestStart, desiredModule),
            };
            Plan addPlan = planner.Plan(desiredSet, currentSet);
            await addPlan.ExecuteAsync(token);

            factory.Recorder.ForEach(r => Assert.Equal(updateExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerRemove1Module()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            IModule removeModule = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
            ModuleSet removeRunning = ModuleSet.Create(removeModule);
            var removeExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestStop, removeModule),
                new TestRecordType(TestCommandType.TestRemove, removeModule),
            };
            Plan addPlan = planner.Plan(ModuleSet.Empty, removeRunning);
            await addPlan.ExecuteAsync(token);

            factory.Recorder.ForEach(r => Assert.Equal(removeExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void RestartPlannerAddRemoveUpdate()
        {
            var factory = new TestCommandFactory();
            var planner = new RestartPlanner(factory);
            var token = new CancellationToken();

            var currentModules = new List<IModule>
            {
                new TestModule("UpdateMod1", "version1", "test", ModuleStatus.Running, Config1),
                new TestModule("UpdateMod2", "version1", "test", ModuleStatus.Stopped, Config1),
                new TestModule("RemoveMod1", "version1", "test", ModuleStatus.Running, Config1),
                new TestModule("RemoveMod2", "version1", "test", ModuleStatus.Stopped, Config1)
            };
            var desiredModules = new List<IModule>
            {
                new TestModule("NewMod1", "version1", "test", ModuleStatus.Running, Config1),
                new TestModule("NewMod2", "version1", "test", ModuleStatus.Stopped, Config1),
                new TestModule("UpdateMod1", "version1", "test", ModuleStatus.Running, Config1),
                new TestModule("UpdateMod2", "version1", "test", ModuleStatus.Stopped, Config1)

            };
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
                new TestRecordType(TestCommandType.TestPull, desiredModules[0]),
                new TestRecordType(TestCommandType.TestPull, desiredModules[1]),
                new TestRecordType(TestCommandType.TestPull, desiredModules[2]),
                new TestRecordType(TestCommandType.TestPull, desiredModules[3]),
                new TestRecordType(TestCommandType.TestCreate, desiredModules[0]),
                new TestRecordType(TestCommandType.TestCreate, desiredModules[1]),
                new TestRecordType(TestCommandType.TestUpdate, desiredModules[2]),
                new TestRecordType(TestCommandType.TestUpdate, desiredModules[3]),
                new TestRecordType(TestCommandType.TestStart, desiredModules[0]),
                new TestRecordType(TestCommandType.TestStart, desiredModules[2]),
            };
            Plan addPlan = planner.Plan(desiredSet, currentSet);
            await addPlan.ExecuteAsync(token);

            //Weak confirmation: no assumed order.
            factory.Recorder.ForEach(recorder => Assert.All(updateExecutionList, r => Assert.True(recorder.ExecutionList.Contains(r))));
            factory.Recorder.ForEach((recorder) =>
               {
                    // One way to validate order
                    // UpdateMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[0])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[8])));
                   Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[8])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[12])));
                   Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[12])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[15])));
                    // UpdateMod2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[1])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[9])));
                   Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[9])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[13])));
                    // RemoveMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[3])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[5])));
                    // RemoveMod2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[4])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])));
                    // AddMod1
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[6])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[10])));
                   Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[10])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[14])));
                    // AddModTrue2
                    Assert.True(recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[7])) < recorder.ExecutionList.FindIndex(r => r.Equals(updateExecutionList[11])));
               });
        }
    }
}