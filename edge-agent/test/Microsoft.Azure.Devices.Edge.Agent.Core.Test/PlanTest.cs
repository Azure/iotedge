// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PlanTest
    {
        [Fact]
        [Unit]
        public async void TestPlanExecution()
        {
            Option<TestPlanRecorder> recordKeeper = Option.Some<TestPlanRecorder>(new TestPlanRecorder());
            List<TestRecordType> moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"))),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module2", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image2"))),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"))),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"))),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"))),

            };
            List <ICommand> commandList = new List<ICommand>
            {
                new TestCommand(moduleExecutionList[0].testType, moduleExecutionList[0].module, recordKeeper),
                new TestCommand(moduleExecutionList[1].testType, moduleExecutionList[1].module, recordKeeper),
                new TestCommand(moduleExecutionList[2].testType, moduleExecutionList[2].module, recordKeeper),
                new TestCommand(moduleExecutionList[3].testType, moduleExecutionList[3].module, recordKeeper),
                new TestCommand(moduleExecutionList[4].testType, moduleExecutionList[4].module, recordKeeper)
            };

            Plan plan1 = new Plan(commandList);
            Plan plan2 = new Plan(new List<ICommand>());

            Assert.False(plan1.IsEmpty);
            Assert.True(Plan.Empty.IsEmpty);
            Assert.True(plan2.IsEmpty);

            CancellationToken token = new CancellationToken();

            await plan1.ExecuteAsync(token);
            Assert.All(commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandExecuted);
                });
            recordKeeper.ForEach( r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestPlanFactoryCommands()
        {
            TestCommandFactory factory = new TestCommandFactory();
            List<TestRecordType> moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"))),
                new TestRecordType(TestCommandType.TestPull, new TestModule("module2", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image2"))),
                new TestRecordType(TestCommandType.TestUpdate, new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"))),
                new TestRecordType(TestCommandType.TestRemove, new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"))),
                new TestRecordType(TestCommandType.TestStart, new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"))),
                new TestRecordType(TestCommandType.TestStop, new TestModule("module6", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image6"))),
            };
            List<ICommand> commandList = new List<ICommand>
            {
                factory.Create(moduleExecutionList[0].module),
                factory.Pull(moduleExecutionList[1].module),
                factory.Update(moduleExecutionList[0].module, moduleExecutionList[2].module),
                factory.Remove(moduleExecutionList[3].module),
                factory.Start(moduleExecutionList[4].module),
                factory.Stop(moduleExecutionList[5].module),
            };
            Plan plan1 = new Plan(commandList);
            CancellationToken token = new CancellationToken();
            await plan1.ExecuteAsync(token);
            Assert.All(commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandExecuted);
                });
            factory.Recorder.ForEach( r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }
    }
}