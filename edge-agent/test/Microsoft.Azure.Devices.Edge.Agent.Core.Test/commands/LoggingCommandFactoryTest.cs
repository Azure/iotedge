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
    using CommandMethodExpr = System.Linq.Expressions.Expression<System.Func<ICommandFactory, ICommand>>;
    using TestExecutionExpr = System.Func<LoggingCommandFactory, ICommand>;

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
        public void TestShow()
        {
            var logFactoryMock = new Mock<ILoggerFactory>();
            var factoryMock = new Mock<ICommandFactory>();
            var moduleIdentity = new Mock<IModuleIdentity>();
            ICommand nullCmd = NullCommandFactory.Instance.Create(new ModuleWithIdentity(TestModule, moduleIdentity.Object));

            factoryMock.Setup(f => f.Create(It.IsAny<IModuleWithIdentity>()))
                .Returns(nullCmd);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = factory.Create(new ModuleWithIdentity(TestModule, moduleIdentity.Object));

            Assert.Equal(create.Show(), nullCmd.Show());

        }

        static readonly TestModule TestModule = new TestModule("module", "version", "test", ModuleStatus.Running, new TestConfig("image"));
        static readonly TestModule UpdateModule = new TestModule("module", "version", "test", ModuleStatus.Running, new TestConfig("image"));
        static readonly TestCommand WrapTargetCommand = new TestCommand(TestCommandType.TestCreate, TestModule);

        static IEnumerable<object[]> CreateTestData()
        {
            var moduleIdentity = new Mock<IModuleIdentity>();
            var testModule = new ModuleWithIdentity(TestModule, moduleIdentity.Object);
            var updateModule = new ModuleWithIdentity(UpdateModule, moduleIdentity.Object);
            // CommandMethodBeingTested - factory command under test
            // Command - command object to be mocked.
            // TestExpr - the expression to execute test.
            (CommandMethodExpr CommandMethodBeingTested, ICommand Command, TestExecutionExpr TestExpr)[] testInputRecords = {
                (
                    f => f.Create(testModule),
                    NullCommandFactory.Instance.Create(testModule),
                    factory => factory.Create(testModule)
                ),
                (
                    f => f.Pull(TestModule),
                    NullCommandFactory.Instance.Pull(TestModule),
                    factory => factory.Pull(TestModule)
                ),
                (
                    f => f.Update(TestModule, updateModule),
                    NullCommandFactory.Instance.Update(TestModule, updateModule),
                    factory => factory.Update(TestModule, updateModule)
                ),
                (
                    f => f.Remove(TestModule),
                    NullCommandFactory.Instance.Remove(TestModule),
                    factory => factory.Remove(TestModule)
                ),
                (
                    f => f.Start(TestModule),
                    NullCommandFactory.Instance.Start(TestModule),
                    factory => factory.Start(TestModule)
                ),
                (
                    f => f.Stop(TestModule),
                    NullCommandFactory.Instance.Stop(TestModule),
                    factory => factory.Stop(TestModule)
                ),
                (
                    f => f.Restart(TestModule),
                    NullCommandFactory.Instance.Restart(TestModule),
                    factory => factory.Restart(TestModule)
                ),
                (
                    f => f.Wrap(WrapTargetCommand),
                    WrapTargetCommand,
                    factory => factory.Wrap(WrapTargetCommand)
                )
            };

            return testInputRecords.Select(r => new object[] { r.CommandMethodBeingTested, r.Command, r.TestExpr }).AsEnumerable();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task ExecuteSuccessfulTests(
            CommandMethodExpr commandMethodBeingTested,
            ICommand commandBeingDecorated,
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
            ICommand create = testExpr(factory);

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
            ICommand commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            factoryMock.Setup(commandMethodBeingTested)
                .Returns(FailureCommand.Instance);
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = testExpr(factory);

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
            ICommand commandBeingDecorated,
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

            ICommand create = testExpr(factory);

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
            ICommand commandBeingDecorated,
            TestExecutionExpr testExpr
        )
        {
            var token = new CancellationToken();

            var logFactoryMock = new Mock<ILoggerFactory>();
            var logMock = new Mock<ILogger<LoggingCommandFactory>>();
            var factoryMock = new Mock<ICommandFactory>();

            factoryMock.Setup(commandMethodBeingTested)
                .Returns(FailureCommand.Instance);
            logFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(logMock.Object);

            var factory = new LoggingCommandFactory(factoryMock.Object, logFactoryMock.Object);

            ICommand create = testExpr(factory);

            await Assert.ThrowsAsync<ArgumentException>(() => create.UndoAsync(token));

            factoryMock.Verify(commandMethodBeingTested);
            logMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
            logMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once);
        }
    }
}
