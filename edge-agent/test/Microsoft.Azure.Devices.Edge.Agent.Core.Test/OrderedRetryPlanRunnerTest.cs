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

    public class OrderedRetryPlanRunnerTest
    {
        [Fact]
        [Unit]
        public void TestCreate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrderedRetryPlanRunner(-1, 10, SystemTime.Instance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrderedRetryPlanRunner(0, 10, SystemTime.Instance));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrderedRetryPlanRunner(5, -1, SystemTime.Instance));
            Assert.Throws<ArgumentNullException>(() => new OrderedRetryPlanRunner(5, 10, null));
            Assert.NotNull(new OrderedRetryPlanRunner(5, 10, SystemTime.Instance));
        }

        [Fact]
        [Unit]
        public async void TestExecuteAsyncInputs()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var plan = new Plan(new List<ICommand>());
            CancellationToken token = CancellationToken.None;

            // Act
            // Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => runner.ExecuteAsync(-2, plan, token)
            );

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => runner.ExecuteAsync(10, null, token)
            );
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncRunsPlanCommands()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatWorks("cmd2"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            // Act
            await runner.ExecuteAsync(1, plan, token);

            // Assert
            commands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Once()));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncRunsPlanCommandsTwiceForSameDeployment()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatWorks("cmd2"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            // Act
            await runner.ExecuteAsync(1, plan, token);
            await runner.ExecuteAsync(1, plan, token);

            // Assert
            commands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(2)));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncRunsPlanCommandsEvenIfOneThrows()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));

            // Assert
            commands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Once()));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncSkipsCommandThatThrowsDuringSecondRun()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await runner.ExecuteAsync(1, plan, token);

            // Assert
            List<Mock<ICommand>> goodCommands = commands.Where(c => c.Object.Id != "badcmd1").ToList();
            goodCommands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(2)));
            commands
                .Except(goodCommands)
                .ToList()
                .ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Once()));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncDoesNotSkipCommandThatThrowsDuringSecondRunWithNewDeployment()
        {
            // Arrange
            var runner = new OrderedRetryPlanRunner(5, 10, SystemTime.Instance);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(2, plan, token));

            // Assert
            commands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(2)));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncRunsSkippedCommandAfterInitialTimeout()
        {
            // Arrange
            var systemTime = new Mock<ISystemTime>();
            const int CoolOffTimeInSeconds = 10;
            var runner = new OrderedRetryPlanRunner(5, CoolOffTimeInSeconds, systemTime.Object);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            DateTime callTime = DateTime.UtcNow;
            systemTime.SetupGet(s => s.UtcNow)
                .Returns(() => callTime)
                .Callback(() => callTime = callTime.AddSeconds(25));

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));

            // Assert
            commands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(2)));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncRunsSkippedCommandAfterSecondTimeout()
        {
            // Arrange
            var systemTime = new Mock<ISystemTime>();
            const int CoolOffTimeInSeconds = 10;
            var runner = new OrderedRetryPlanRunner(5, CoolOffTimeInSeconds, systemTime.Object);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            DateTime callTime = DateTime.UtcNow;
            systemTime.SetupGet(s => s.UtcNow)
                .Returns(() => callTime)
                .Callback(() => callTime = callTime.AddSeconds(25));

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await runner.ExecuteAsync(1, plan, token);
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));

            // Assert
            List<Mock<ICommand>> goodCommands = commands.Where(c => c.Object.Id != "badcmd1").ToList();
            goodCommands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(4)));
            commands
                .Except(goodCommands)
                .ToList()
                .ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(3)));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncGivesUpOnCommandAfterHittingMaxRetries()
        {
            // Arrange
            var systemTime = new Mock<ISystemTime>();
            const int CoolOffTimeInSeconds = 10;
            const int MaxRunCount = 2;
            var runner = new OrderedRetryPlanRunner(MaxRunCount, CoolOffTimeInSeconds, systemTime.Object);
            var commands = new List<Mock<ICommand>>
            {
                this.MakeMockCommandThatWorks("cmd1"),
                this.MakeMockCommandThatThrows("badcmd1"),
                this.MakeMockCommandThatWorks("cmd3")
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            DateTime callTime = DateTime.UtcNow;
            systemTime.SetupGet(s => s.UtcNow)
                .Returns(() => callTime)
                .Callback(() => callTime = callTime.AddSeconds(25));

            // Act
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await runner.ExecuteAsync(1, plan, token);
            await runner.ExecuteAsync(1, plan, token);

            // Assert
            List<Mock<ICommand>> goodCommands = commands.Where(c => c.Object.Id != "badcmd1").ToList();
            goodCommands.ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(4)));
            commands
                .Except(goodCommands)
                .ToList()
                .ForEach(mc => mc.Verify(c => c.ExecuteAsync(token), Times.Exactly(2)));
        }

        [Fact]
        [Unit]
        public async void ExecuteAsyncResetsStatsOnFailingCommandOnceItSucceeds()
        {
            // Arrange
            var systemTime = new Mock<ISystemTime>();
            const int CoolOffTimeInSeconds = 10;
            const int MaxRunCount = 2;
            var runner = new OrderedRetryPlanRunner(MaxRunCount, CoolOffTimeInSeconds, systemTime.Object);
            Mock<ICommand> goodCommand = this.MakeMockCommandThatWorks("cmd1");
            Mock<ICommand> badCommand = this.MakeMockCommandThatThrows("badcmd1");
            var commands = new List<Mock<ICommand>>
            {
                goodCommand, badCommand
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            CancellationToken token = CancellationToken.None;

            DateTime callTime = DateTime.UtcNow;
            systemTime.SetupGet(s => s.UtcNow)
                .Returns(() => callTime)
                .Callback(() => callTime = callTime.AddSeconds(25));

            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));

            // make it so that the bad command runs fine now
            badCommand.Setup(c => c.ExecuteAsync(token)).Returns(Task.CompletedTask);

            // Act
            await runner.ExecuteAsync(1, plan, token);

            // now if we have the command fail, the retry count should be 1 which
            // means that during yet another run it should get executed after another 20 seconds
            badCommand.Setup(c => c.ExecuteAsync(token)).ThrowsAsync(new InvalidOperationException("No donuts for you"));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));
            await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(1, plan, token));

            // Assert
            goodCommand.Verify(c => c.ExecuteAsync(token), Times.Exactly(4));
            badCommand.Verify(c => c.ExecuteAsync(token), Times.Exactly(4));
        }

        Mock<ICommand> MakeMockCommandThatWorks(string id, Action callback = null)
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

        Mock<ICommand> MakeMockCommandThatThrows(string id)
        {
            var command = new Mock<ICommand>();
            command.SetupGet(c => c.Id).Returns(id);
            command.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("No donuts for you"));
            command.Setup(c => c.Show())
                .Returns(id);
            return command;
        }

        [Fact]
        [Unit]
        public async void TestOrderedRetryPlanRunnerCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Mock<ICommand>[] commands = new[]
            {
                this.MakeMockCommandThatWorks("c1"),
                this.MakeMockCommandThatWorks("c2", () => cts.Cancel()),
                this.MakeMockCommandThatWorks("c3"),
            };
            var plan = new Plan(commands.Select(c => c.Object).ToList());
            var systemTime = new Mock<ISystemTime>();
            const int CoolOffTimeInSeconds = 10;
            const int MaxRunCount = 2;
            var runner = new OrderedRetryPlanRunner(MaxRunCount, CoolOffTimeInSeconds, systemTime.Object);

            // Act
            await runner.ExecuteAsync(1, plan, cts.Token);

            // Assert
            commands[0].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[1].Verify(m => m.ExecuteAsync(cts.Token), Times.Once());
            commands[2].Verify(m => m.ExecuteAsync(cts.Token), Times.Never());
        }
    }
}
