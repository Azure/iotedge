// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class OrderedPlanRunnerTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();

        [Fact]
        [Unit]
        public async void TestPlanExecution()
        {
            Option<TestPlanRecorder> recordKeeper = Option.Some(new TestPlanRecorder());
            var moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module2", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image2"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
            };
            var commandList = new List<ICommand>
            {
                new TestCommand(moduleExecutionList[0].TestType, moduleExecutionList[0].Module, recordKeeper),
                new TestCommand(moduleExecutionList[1].TestType, moduleExecutionList[1].Module, recordKeeper),
                new TestCommand(moduleExecutionList[2].TestType, moduleExecutionList[2].Module, recordKeeper),
                new TestCommand(moduleExecutionList[3].TestType, moduleExecutionList[3].Module, recordKeeper),
                new TestCommand(moduleExecutionList[4].TestType, moduleExecutionList[4].Module, recordKeeper)
            };

            var plan1 = new Plan(commandList);
            var plan2 = new Plan(new List<ICommand>());

            Assert.False(plan1.IsEmpty);
            Assert.True(Plan.Empty.IsEmpty);
            Assert.True(plan2.IsEmpty);

            var token = new CancellationToken();

            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan1, token);
            Assert.All(
                commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandExecuted);
                });
            recordKeeper.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestPlanFactoryCommands()
        {
            var factory = new TestCommandFactory();
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestUpdate, new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestRemove, new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestStart, new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestStop, new TestModule("module6", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image6"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
            };
            var identity = new Mock<IModuleIdentity>();
            var commandList = new List<ICommand>
            {
                await factory.CreateAsync(new ModuleWithIdentity(moduleExecutionList[0].Module, identity.Object), runtimeInfo),
                await factory.UpdateAsync(moduleExecutionList[0].Module, new ModuleWithIdentity(moduleExecutionList[1].Module, identity.Object), runtimeInfo),
                await factory.RemoveAsync(moduleExecutionList[2].Module),
                await factory.StartAsync(moduleExecutionList[3].Module),
                await factory.StopAsync(moduleExecutionList[4].Module),
            };
            var plan1 = new Plan(commandList);
            var token = new CancellationToken();
            var planRunner = new OrderedPlanRunner();
            await planRunner.ExecuteAsync(1, plan1, token);
            Assert.All(
                commandList,
                command =>
                {
                    var c = command as TestCommand;
                    Assert.NotNull(c);
                    Assert.True(c.CommandExecuted);
                });
            factory.Recorder.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestPlanContinueOnFailure()
        {
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var factory = new TestCommandFactory();
            var failureFactory = new TestCommandFailureFactory();
            var moduleExecutionList = new List<TestRecordType>
            {
                new TestRecordType(TestCommandType.TestCreate, new TestModule("module1", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image1"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestUpdate, new TestModule("module3", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image3"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestRemove, new TestModule("module4", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image4"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestStart, new TestModule("module5", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image5"), RestartPolicy.OnUnhealthy, ImagePullPolicy.Never, DefaultConfigurationInfo, EnvVars)),
                new TestRecordType(TestCommandType.TestStop, new TestModule("module6", "version1", "type1", ModuleStatus.Stopped, new TestConfig("image6"), RestartPolicy.OnUnhealthy, ImagePullPolicy.Never, DefaultConfigurationInfo, EnvVars)),
            };
            var identity = new Mock<IModuleIdentity>();
            var commandList = new List<ICommand>
            {
                await failureFactory.CreateAsync(new ModuleWithIdentity(moduleExecutionList[0].Module, identity.Object), runtimeInfo),
                await factory.CreateAsync(new ModuleWithIdentity(moduleExecutionList[0].Module, identity.Object), runtimeInfo),
                await failureFactory.UpdateAsync(moduleExecutionList[0].Module, new ModuleWithIdentity(moduleExecutionList[1].Module, identity.Object), runtimeInfo),
                await factory.UpdateAsync(moduleExecutionList[0].Module, new ModuleWithIdentity(moduleExecutionList[1].Module, identity.Object), runtimeInfo),
                await failureFactory.RemoveAsync(moduleExecutionList[2].Module),
                await factory.RemoveAsync(moduleExecutionList[2].Module),
                await failureFactory.StartAsync(moduleExecutionList[3].Module),
                await factory.StartAsync(moduleExecutionList[3].Module),
                await failureFactory.StopAsync(moduleExecutionList[4].Module),
                await factory.StopAsync(moduleExecutionList[4].Module),
            };
            var plan1 = new Plan(commandList);
            var token = new CancellationToken();
            var planRunner = new OrderedPlanRunner();
            AggregateException ex = await Assert.ThrowsAsync<AggregateException>(async () => await planRunner.ExecuteAsync(1, plan1, token));

            Assert.True(ex.InnerExceptions.Count == commandList.Count / 2);
            Assert.True(
                commandList.Where(
                    command =>
                    {
                        var c = command as TestCommand;
                        Assert.NotNull(c);
                        return c.CommandExecuted;
                    }).Count() == commandList.Count / 2);
            Assert.True(
                commandList.Where(
                    command =>
                    {
                        var c = command as TestCommand;
                        Assert.NotNull(c);
                        return !c.CommandExecuted;
                    }).Count() == commandList.Count / 2);

            factory.Recorder.ForEach(r => Assert.Equal(moduleExecutionList, r.ExecutionList));
        }

        [Fact]
        [Unit]
        public async void TestOrderedPlanRunnerCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Mock<ICommand>[] commands =
            {
                this.MakeMockCommand("c1"),
                this.MakeMockCommand("c2", () => cts.Cancel()),
                this.MakeMockCommand("c3"),
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            var planRunner = new OrderedPlanRunner();

            // Act
            await planRunner.ExecuteAsync(1, plan, cts.Token);

            // Assert
            commands[0].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[1].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[2].Verify(m => m.ExecuteAsync(cts.Token), Times.Never());
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
