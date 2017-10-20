// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Moq;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using CommandMethodExpr = System.Linq.Expressions.Expression<System.Func<ICommandFactory, System.Threading.Tasks.Task<ICommand>>>;
    using TestExecutionExpr = System.Func<LoggingCommandFactory, System.Threading.Tasks.Task<ICommand>>;

    class FailureCommand : ICommand
    {
        public static FailureCommand Instance { get; } = new FailureCommand();

        FailureCommand()
        {
        }

        public Task ExecuteAsync(CancellationToken token) => throw new ArgumentException();

        public Task UndoAsync(CancellationToken token) => throw new ArgumentException();

        public string Show() => "[Failure]";
    }

    public class LoggingCommandFactoryTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");

        [Fact]
        [Unit]
        public void InvalidParamtersForConstructor()
        {
            var loggingFactory = Mock.Of<ILoggerFactory>();
            var underlyingFactory = Mock.Of<ICommandFactory>();

            Assert.Throws<ArgumentNullException>(() => new LoggingCommandFactory(null, loggingFactory));
            Assert.Throws<ArgumentNullException>(() => new LoggingCommandFactory(underlyingFactory, null));
        }

        [Fact]
        [Unit]
        public async void TestShow()
        {
            var logFactoryMock = new Mock<ILoggerFactory>();
            var factoryMock = new Mock<ICommandFactory>();
            var moduleIdentity = new Mock<IModuleIdentity>();
            Task<ICommand> nullCmd = NullCommandFactory.Instance.CreateAsync(new ModuleWithIdentity(TestModule, moduleIdentity.Object));

            factoryMock.Setup(f => f.CreateAsync(It.IsAny<IModuleWithIdentity>()))
                .Returns(nullCmd);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = await factory.CreateAsync(new ModuleWithIdentity(TestModule, moduleIdentity.Object));

            Assert.Equal(create.Show(), nullCmd.Result.Show());

        }

        static readonly TestModule TestModule = new TestModule("module", "version", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);
        static readonly TestModule UpdateModule = new TestModule("module", "version", "test", ModuleStatus.Running, new TestConfig("image"), RestartPolicy.OnUnhealthy, DefaultConfigurationInfo);
        static readonly TestCommand WrapTargetCommand = new TestCommand(TestCommandType.TestCreate, TestModule);

        static IEnumerable<object[]> CreateTestData()
        {
            var moduleIdentity = new Mock<IModuleIdentity>();
            var testModule = new ModuleWithIdentity(TestModule, moduleIdentity.Object);
            var updateModule = new ModuleWithIdentity(UpdateModule, moduleIdentity.Object);
            // CommandMethodBeingTested - factory command under test
            // Command - command object to be mocked.
            // TestExpr - the expression to execute test.
            (CommandMethodExpr CommandMethodBeingTested, Task<ICommand> Command, TestExecutionExpr TestExpr)[] testInputRecords = {
                (
                    f => f.CreateAsync(testModule),
                    NullCommandFactory.Instance.CreateAsync(testModule),
                    factory => factory.CreateAsync(testModule)
                ),
                (
                    f => f.PullAsync(TestModule),
                    NullCommandFactory.Instance.PullAsync(TestModule),
                    factory => factory.PullAsync(TestModule)
                ),
                (
                    f => f.UpdateAsync(TestModule, updateModule),
                    NullCommandFactory.Instance.UpdateAsync(TestModule, updateModule),
                    factory => factory.UpdateAsync(TestModule, updateModule)
                ),
                (
                    f => f.RemoveAsync(TestModule),
                    NullCommandFactory.Instance.RemoveAsync(TestModule),
                    factory => factory.RemoveAsync(TestModule)
                ),
                (
                    f => f.StartAsync(TestModule),
                    NullCommandFactory.Instance.StartAsync(TestModule),
                    factory => factory.StartAsync(TestModule)
                ),
                (
                    f => f.StopAsync(TestModule),
                    NullCommandFactory.Instance.StopAsync(TestModule),
                    factory => factory.StopAsync(TestModule)
                ),

                (
                    f => f.RestartAsync(TestModule),
                    NullCommandFactory.Instance.RestartAsync(TestModule),
                    factory => factory.RestartAsync(TestModule)
                ),
                (
                    f => f.WrapAsync(WrapTargetCommand),
                    Task.FromResult<ICommand>(WrapTargetCommand),
                    factory => factory.WrapAsync(WrapTargetCommand)
                )
            };

            return testInputRecords.Select(r => new object[] { r.CommandMethodBeingTested, r.Command, r.TestExpr }).AsEnumerable();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task ExecuteSuccessfulTests(
            CommandMethodExpr commandMethodBeingTested,
            Task<ICommand> commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            // mock the command factory method being tested, 
            // have the mock return an appropriate command that should be decorated by the LoggingCommandFactory
            factoryMock.Setup(commandMethodBeingTested)
                .Returns(commandBeingDecorated);
            // use this ILogger mock for verification.
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            // Execute the test expression
            ICommand create = await testExpr(factory);

            // attempt to execute the LoggingCommand we received
            await create.ExecuteAsync(token);

            //Assert decorated command is executed, and command is logged.
            factoryMock.Verify(commandMethodBeingTested);
            logMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
            logMock.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task ExecuteFailureTests(
            CommandMethodExpr commandMethodBeingTested,
            Task<ICommand> commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            factoryMock.Setup(commandMethodBeingTested)
                .Returns(Task.FromResult<ICommand>(FailureCommand.Instance));
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = await testExpr(factory);

            await Assert.ThrowsAsync<ArgumentException>(() => create.ExecuteAsync(token));

            factoryMock.Verify(commandMethodBeingTested);
            logMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
            logMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task UndoSuccessTests(
            CommandMethodExpr commandMethodBeingTested,
            Task<ICommand> commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            factoryMock.Setup(commandMethodBeingTested)
                .Returns(commandBeingDecorated);
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = await testExpr(factory);

            await create.UndoAsync(token);

            factoryMock.Verify(commandMethodBeingTested);
            logMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
            logMock.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task UndoFailureTests(
            CommandMethodExpr commandMethodBeingTested,
            Task<ICommand> commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            factoryMock.Setup(commandMethodBeingTested)
                .Returns(Task.FromResult<ICommand>(FailureCommand.Instance));
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = await testExpr(factory);

            await Assert.ThrowsAsync<ArgumentException>(() => create.UndoAsync(token));

            factoryMock.Verify(commandMethodBeingTested);
            logMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
            logMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
        }
    }
}
