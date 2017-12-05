// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class GroupCommandTest
    {
        static IEnumerable<object[]> CreateTestData()
        {
            ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo();

            Option<TestPlanRecorder> recordKeeper1 = Option.Some(new TestPlanRecorder());
            Option<TestPlanRecorder> recordKeeper2 = Option.Some(new TestPlanRecorder());
            Option<TestPlanRecorder> recordKeeper3 = Option.Some(new TestPlanRecorder());
            List<TestCommandType> tt = new List<TestCommandType>
            {
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate
            };
            List<TestModule> tm = new List<TestModule>{
                new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo),
                new TestModule("module2", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image2"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo),
                new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo),
                new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo),
                new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo)
            };
            (Option<TestPlanRecorder> recorder, List<TestRecordType> moduleExecutionList, List<ICommand> commandList)[] testInputRecords =
            {
                (
                    recordKeeper1,
                    new List<TestRecordType>
                    {
                        new TestRecordType(tt[0], tm[0])
                    },
                    new List<ICommand>
                    {
                        new TestCommand(tt[0], tm[0], recordKeeper1)
                    }
                ),
                (
                    recordKeeper2,
                    new List<TestRecordType>
                    {
                        new TestRecordType(tt[1], tm[1]),
                        new TestRecordType(tt[2], tm[2]),
                        new TestRecordType(tt[3], tm[3]),
                        new TestRecordType(tt[4], tm[4])
                    },
                    new List<ICommand>
                    {
                        new TestCommand(tt[1], tm[1], recordKeeper2),
                        new TestCommand(tt[2], tm[2], recordKeeper2),
                        new TestCommand(tt[3], tm[3], recordKeeper2),
                        new TestCommand(tt[4], tm[4], recordKeeper2)
                    }
                ),
                (
                recordKeeper3,
                new List<TestRecordType>(),
                new List<ICommand>()
                )
            };
            {
                return testInputRecords.Select(r => new object[] { r.recorder, r.moduleExecutionList, r.commandList }).AsEnumerable();
            }
        }

        [Fact]
        [Unit]
        public async Task CreateThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GroupCommand(null));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await GroupCommand.CreateAsync(null));
        }

        void AssertCommands(Option<TestPlanRecorder> recordKeeper, List<ICommand> commandList, List<TestRecordType> recordlist)
        {
            Assert.All(commandList, command =>
            {
                var c = command as TestCommand;
                Assert.NotNull(c);
                Assert.True(c.CommandExecuted);
            });
            recordKeeper.ForEach(r => Assert.Equal(recordlist, r.ExecutionList));
        }

        void AssertUndo(Option<TestPlanRecorder> recordKeeper, List<ICommand> commandList, List<TestRecordType> recordlist)
        {
            Assert.All(commandList, command =>
            {
                var c = command as TestCommand;
                Assert.NotNull(c);
                Assert.True(c.CommandUndone);
            });
            recordKeeper.ForEach(r => Assert.Equal(recordlist, r.UndoList));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task GroupCommandTestCreate(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList
            )
        {
            GroupCommand g = new GroupCommand(commandList.ToArray());

            var token = new CancellationToken();

            await g.ExecuteAsync(token);

            this.AssertCommands(recorder, commandList, moduleExecutionList);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task GroupCommandTestCreateAsync(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList
            )
        {
            ICommand g = await GroupCommand.CreateAsync(commandList.ToArray());

            var token = new CancellationToken();

            await g.ExecuteAsync(token);

            this.AssertCommands(recorder, commandList, moduleExecutionList);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task GroupCommandTestUndoAsync(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList
            )
        {
            ICommand g = await GroupCommand.CreateAsync(commandList.ToArray());

            var token = new CancellationToken();

            await g.UndoAsync(token);

            this.AssertUndo(recorder, commandList, moduleExecutionList);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task GroupCommandTestShow(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList
        )
        {
            ICommand g = await GroupCommand.CreateAsync(commandList.ToArray());

            string showString = g.Show();

            foreach(var command in commandList)
            {
                Assert.True(showString.Contains(command.Show()));
            }
        }
    }
}
