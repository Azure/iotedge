// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class GroupCommandTest
    {
        [Fact]
        [Unit]
        public void CreateThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GroupCommand(null));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task TestCreate(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList)
        {
            var g = new GroupCommand(commandList.ToArray());

            var token = new CancellationToken();

            await g.ExecuteAsync(token);

            this.AssertCommands(recorder, commandList, moduleExecutionList);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task TestUndoAsync(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList)
        {
            ICommand g = new GroupCommand(commandList.ToArray());

            var token = new CancellationToken();

            await g.UndoAsync(token);

            this.AssertUndo(recorder, commandList, moduleExecutionList);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public void TestShow(
            Option<TestPlanRecorder> recorder,
            List<TestRecordType> moduleExecutionList,
            List<ICommand> commandList)
        {
            ICommand g = new GroupCommand(commandList.ToArray());

            string showString = g.Show();

            foreach (ICommand command in commandList)
            {
                Assert.Contains(command.Show(), showString);
            }
        }

        [Fact]
        [Unit]
        public async Task TestGroupCommandCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Mock<ICommand>[] commands =
            {
                this.MakeMockCommand("c1"),
                this.MakeMockCommand("c2", () => cts.Cancel()),
                this.MakeMockCommand("c3"),
            };
            ICommand groupCommand = new GroupCommand(
                commands.Select(m => m.Object).ToArray());

            // Act
            await groupCommand.ExecuteAsync(cts.Token);

            // Assert
            commands[0].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[1].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[2].Verify(m => m.ExecuteAsync(cts.Token), Times.Never());
        }

        public static IEnumerable<object[]> CreateTestData()
        {
            var defaultConfigurationInfo = new ConfigurationInfo();
            IDictionary<string, EnvVal> envVars = new Dictionary<string, EnvVal>();
            Option<TestPlanRecorder> recordKeeper1 = Option.Some(new TestPlanRecorder());
            Option<TestPlanRecorder> recordKeeper2 = Option.Some(new TestPlanRecorder());
            Option<TestPlanRecorder> recordKeeper3 = Option.Some(new TestPlanRecorder());
            var tt = new List<TestCommandType>
            {
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate,
                TestCommandType.TestCreate
            };
            var tm = new List<TestModule>
            {
                new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, defaultConfigurationInfo, envVars),
                new TestModule("module2", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image2"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, defaultConfigurationInfo, envVars),
                new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, defaultConfigurationInfo, envVars),
                new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, defaultConfigurationInfo, envVars),
                new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, defaultConfigurationInfo, envVars)
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

        // Disabling this reSharper Error. commandList is being used on Assert All.
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        void AssertCommands(Option<TestPlanRecorder> recordKeeper, List<ICommand> commandList, List<TestRecordType> recordlist)
        {
            Assert.All(
                commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandExecuted);
                });
            recordKeeper.ForEach(r => Assert.Equal(recordlist, r.ExecutionList));
        }

        // Disabling this reSharper Error. commandList is being used on Assert All.
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        void AssertUndo(Option<TestPlanRecorder> recordKeeper, List<ICommand> commandList, List<TestRecordType> recordlist)
        {
            Assert.All(
                commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandUndone);
                });
            recordKeeper.ForEach(r => Assert.Equal(recordlist, r.UndoList));
        }

        Mock<ICommand> MakeMockCommand(string id, Action callback = null)
        {
            callback = callback ?? (() => { });

            var command = new Mock<ICommand>();
            command.SetupGet(c => c.Id).Returns(id);
            command.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .Callback(callback)
                .Returns(Task.CompletedTask);
            command.Setup(c => c.Show())
                .Returns(id);
            return command;
        }
    }
}
