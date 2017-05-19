// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints.StateMachine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Moq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class EndpointExecutorFsmTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3, 4}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 1);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] {2, 3, 4, 1}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 2);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] {3, 4, 1, 2}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 3);
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] {4, 1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, 4);

        static readonly RetryStrategy MaxRetryStrategy = new FixedInterval(int.MaxValue, TimeSpan.FromMilliseconds(int.MaxValue));
        static readonly EndpointExecutorConfig MaxConfig = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, MaxRetryStrategy, TimeSpan.FromMinutes(5));

        static IMessage MessageWithOffset(long offset) =>
            new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string>(), offset);

        [Fact, Unit]
        public async Task TestCheckpoint()
        {
            // Test checkpoint
            var endpoint1 = new TestEndpoint("id1");
            var checkpointer = new Mock<ICheckpointer>();
            checkpointer.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            var machine1 = new EndpointExecutorFsm(endpoint1, checkpointer.Object, MaxConfig);
            await machine1.RunAsync(Commands.SendMessage(Message1, Message2));
            checkpointer.Verify(c => c.CommitAsync(new [] { Message1, Message2 }, new IMessage[0], Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            await machine1.CloseAsync();

            // Test no checkpoint
            var endpoint2 = new NullEndpoint("id2");
            var checkpointer2 = new Mock<ICheckpointer>();
            checkpointer2.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(false);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            var machine2 = new EndpointExecutorFsm(endpoint2, checkpointer2.Object, MaxConfig);
            await machine2.RunAsync(Commands.SendMessage(Message1));
            checkpointer2.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), Option.None<DateTime>(), Option.None<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            await machine2.CloseAsync();
        }

        [Fact, Unit]
        public async Task TestCheckpointAdmit()
        {
            var endpoint = new TestEndpoint("id1");

            var dao = new Mock<ICheckpointStore>();
            dao.Setup(d => d.GetCheckpointDataAsync("id1", It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CheckpointData(10L)));

            using (ICheckpointer checkpointer = await Checkpointer.CreateAsync("id1", dao.Object))
            using (var machine = new EndpointExecutorFsm(endpoint, checkpointer, MaxConfig))
            {
                await machine.RunAsync(Commands.SendMessage(MessageWithOffset(1), MessageWithOffset(2), MessageWithOffset(3)));
                dao.Verify(d => d.SetCheckpointDataAsync(It.IsAny<string>(), new CheckpointData(It.IsAny<long>()), It.IsAny<CancellationToken>()), Times.Never);
                Assert.Equal(10L, checkpointer.Offset);
                Assert.Equal(new List<IMessage>(), endpoint.Processed);

                await machine.RunAsync(Commands.SendMessage(MessageWithOffset(12), MessageWithOffset(8), MessageWithOffset(13)));
                Assert.Equal(13, checkpointer.Offset);
                Expression<Func<CheckpointData, bool>> expr = obj => obj.Offset == 13;
                dao.Verify(d => d.SetCheckpointDataAsync("id1", It.Is(expr), It.IsAny<CancellationToken>()), Times.Exactly(1));
                Assert.Equal(new List<IMessage> { MessageWithOffset(12), MessageWithOffset(13) }, endpoint.Processed);

                await machine.CloseAsync();
                await checkpointer.CloseAsync(CancellationToken.None);
            }
        }

        [Fact, Unit]
        public async Task TestCheckpointDead()
        {
            var endpoint = new FailedEndpoint("id1", new Exception());

            var dao = new Mock<ICheckpointStore>();
            dao.Setup(d => d.GetCheckpointDataAsync("id1", It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CheckpointData(10L)));

            var retryStrategy = new FixedInterval(0, TimeSpan.FromMilliseconds(int.MaxValue));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.MaxValue);

            using (ICheckpointer checkpointer = await Checkpointer.CreateAsync("id1", dao.Object))
            using (var machine = new EndpointExecutorFsm(endpoint, checkpointer, config))
            {
                await machine.RunAsync(Commands.SendMessage(MessageWithOffset(11)));
                Assert.Equal(State.DeadIdle, machine.Status.State);
                Assert.Equal(11L, checkpointer.Offset);
                Expression<Func<CheckpointData, bool>> expr1 = obj => obj.Offset == 11;
                dao.Verify(d => d.SetCheckpointDataAsync("id1", It.Is(expr1), It.IsAny<CancellationToken>()), Times.Exactly(1));

                await machine.RunAsync(Commands.SendMessage(MessageWithOffset(1), MessageWithOffset(2), MessageWithOffset(3)));
                Assert.Equal(State.DeadIdle, machine.Status.State);
                Assert.Equal(11L, checkpointer.Offset);
                dao.Verify(d => d.SetCheckpointDataAsync(It.IsAny<string>(), new CheckpointData(It.IsIn(1, 2, 3)), It.IsAny<CancellationToken>()), Times.Never);

                await machine.RunAsync(Commands.SendMessage(MessageWithOffset(12), MessageWithOffset(8), MessageWithOffset(13)));
                Assert.Equal(State.DeadIdle, machine.Status.State);
                Assert.Equal(13, checkpointer.Offset);
                Expression<Func<CheckpointData, bool>> expr2 = obj => obj.Offset == 13;
                dao.Verify(d => d.SetCheckpointDataAsync("id1", It.Is(expr2), It.IsAny<CancellationToken>()), Times.Exactly(1));

                await machine.CloseAsync();
                await checkpointer.CloseAsync(CancellationToken.None);
            }
        }

        [Fact, Unit]
        public async Task TestCheckpointException()
        {
            // Test no throw
            var endpoint1 = new TestEndpoint("id2");
            var checkpointer1 = new Mock<ICheckpointer>();
            checkpointer1.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer1.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            checkpointer1.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            checkpointer1.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());

            var retryStrategy = new FixedInterval(1, TimeSpan.FromSeconds(1));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            var machine1 = new EndpointExecutorFsm(endpoint1, checkpointer1.Object, config);
            SendMessage command1 = Commands.SendMessage(Message1);
            await machine1.RunAsync(command1);
            await command1.Completion;

            Assert.Equal(State.Idle, machine1.Status.State);
            checkpointer1.Verify(c => c.CommitAsync(new[] { Message1 }, new IMessage[0], It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            await machine1.CloseAsync();

            // Test exception throws
            var endpoint2 = new TestEndpoint("id2");
            var checkpointer2 = new Mock<ICheckpointer>();
            checkpointer2.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());

            var config2 = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5), true);
            var machine2 = new EndpointExecutorFsm(endpoint2, checkpointer2.Object, config2);
            SendMessage command2 = Commands.SendMessage(Message1);
            await machine2.RunAsync(command2);
            await Assert.ThrowsAsync<Exception>(() => command2.Completion);
            Assert.Equal(State.Idle, machine2.Status.State);
            checkpointer2.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            await machine2.CloseAsync();

            // Test partial exception - no throw
            var endpoint3 = new PartialFailureEndpoint("id3", new InvalidOperationException("test"));
            var checkpointer3 = new Mock<ICheckpointer>();
            checkpointer3.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer3.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            checkpointer3.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());

            var machine3 = new EndpointExecutorFsm(endpoint3, checkpointer3.Object, config);
            SendMessage command3 = Commands.SendMessage(Message1, Message2);
            await machine3.RunAsync(command3);
            Assert.Equal(State.Failing, machine3.Status.State);
            await machine3.RunAsync(Commands.Retry);
            await command3.Completion;

            Assert.Equal(State.Idle, machine3.Status.State);
            checkpointer3.Verify(c => c.CommitAsync(It.Is<ICollection<IMessage>>(m => m.Count == 1), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            await machine3.CloseAsync();
        }

        [Fact, Unit]
        public async Task TestCheckpointPartialFailureToDead()
        {
            var endpoint1 = new PartialFailureEndpoint("id1", new InvalidOperationException("test"));
            var checkpointer1 = new LoggedCheckpointer(await Checkpointer.CreateAsync("id1", new NullCheckpointStore()));

            var retryStrategy = new FixedInterval(1, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine1 = new EndpointExecutorFsm(endpoint1, checkpointer1, config))
            {
                SendMessage command1 = Commands.SendMessage(Message1, Message3, Message2, Message4);
                await machine1.RunAsync(command1);
                await command1.Completion;

                // TransitionAction happens before next state transition therefore add a small delay
                await Task.Delay(TimeSpan.FromMilliseconds(1));

                Assert.Equal(State.DeadIdle, machine1.Status.State);
                Assert.Equal(new List<IMessage> { Message1, Message3, Message2, Message4 }, checkpointer1.Processed);
                await machine1.CloseAsync();
            }
        }


        [Fact, Unit]
        public async Task TestCheckpointPartialFailureToSuccess()
        {
            var endpoint1 = new PartialFailureEndpoint("id1", new InvalidOperationException("test"));
            var checkpointer1 = new LoggedCheckpointer(await Checkpointer.CreateAsync("id1", new NullCheckpointStore()));

            var retryStrategy = new FixedInterval(4, TimeSpan.FromMilliseconds(100));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine1 = new EndpointExecutorFsm(endpoint1, checkpointer1, config))
            {
                SendMessage command1 = Commands.SendMessage(Message1, Message3, Message2, Message4);
                await machine1.RunAsync(command1);
                await command1.Completion;
                await Task.Delay(50);

                Assert.Equal(State.Idle, machine1.Status.State);
                Assert.Equal(new List<IMessage> { Message1, Message3, Message2, Message4 }, checkpointer1.Processed);
                await machine1.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id1");
            var endpoint3 = new TestEndpoint("id3");

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), MaxConfig))
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => machine.RunAsync(Commands.UpdateEndpoint(null)));

                Assert.Equal(new List<Message>(), endpoint1.Processed);
                Assert.Equal(new List<Message>(), endpoint2.Processed);
                Assert.Equal(new List<Message>(), endpoint3.Processed);

                await machine.RunAsync(Commands.SendMessage(Message1));
                await machine.RunAsync(Commands.SendMessage(Message1));

                await machine.RunAsync(Commands.UpdateEndpoint(endpoint2));

                await machine.RunAsync(Commands.SendMessage(Message2));
                await machine.RunAsync(Commands.SendMessage(Message3));

                Assert.Equal(State.Idle, machine.Status.State);

                await machine.CloseAsync();
                Assert.Equal(State.Closed, machine.Status.State);

                var expected1 = new List<IMessage> { Message1, Message1 };
                var expected2 = new List<IMessage> { Message2, Message3 };
                Assert.Equal(expected1, endpoint1.Processed);
                Assert.Equal(expected2, endpoint2.Processed);
                Assert.Equal(new List<IMessage>(), endpoint3.Processed);
                await Assert.ThrowsAsync<InvalidOperationException>(() => machine.RunAsync(Commands.UpdateEndpoint(endpoint1)));
            }
        }

        [Fact, Unit]
        public async Task TestFailingEndpoint()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new Incremental(int.MaxValue, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);
                Assert.Equal(TimeSpan.FromSeconds(1), machine.Status.RetryPeriod);

                for (int i = 2; i < 10; i++)
                {
                    await machine.RunAsync(Commands.Retry);
                    status = machine.Status;
                    Assert.Equal(State.Failing, status.State);
                    Assert.Equal(i, status.RetryAttempts);
                    Assert.Equal(TimeSpan.FromSeconds(i), machine.Status.RetryPeriod);
                }

                await Assert.ThrowsAsync<InvalidOperationException>(() => machine.RunAsync(Commands.SendMessage(Message2)));
                await Assert.ThrowsAsync<InvalidOperationException>(() => machine.RunAsync(Commands.Succeed));
                await Assert.ThrowsAsync<InvalidOperationException>(() => machine.RunAsync(Commands.Fail(TimeSpan.FromMilliseconds(int.MaxValue))));

                await machine.CloseAsync();
                status = machine.Status;
                Assert.Equal(State.Closed, status.State);
            }
        }

        [Fact, Unit]
        public async Task TestFailingEndpointUpdate()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var endpoint2 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var endpoint3 = new TestEndpoint("id1");

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), MaxConfig))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                // Send message on failing endpoint
                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                // Replace with another failing endpoint
                // attempts should be reset to zero (and then increment after the retry fails)
                await machine.RunAsync(Commands.UpdateEndpoint(endpoint2));
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                // Replace with healthy endpoint
                // Message1 should be delivered and attempts reset
                await machine.RunAsync(Commands.UpdateEndpoint(endpoint3));
                status = machine.Status;

                await command.Completion;

                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);
                Assert.Equal(new List<IMessage> { Message1 }, endpoint3.Processed);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestFailingEndpointClose()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), MaxConfig))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                // Send message on failing endpoint
                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                // Send close command. Should drop message and close
                await machine.RunAsync(Commands.Close);

                await Task.WhenAny(command.Completion, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));

                status = machine.Status;
                Assert.Equal(State.Closed, status.State);
                Assert.Equal(0, status.RetryAttempts);
            }
        }

        [Fact, Unit]
        public async Task TestTimeoutIsFail()
        {
            var endpoint = new StalledEndpoint("id1");
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(1));
            var config = new EndpointExecutorConfig(TimeSpan.FromMilliseconds(100), retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint, new NullCheckpointer(), config))
            {
                await machine.RunAsync(Commands.SendMessage(Message1));

                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestDying()
        {
            var endpoint1 = new RevivableEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                // Set endpoint to always fail
                endpoint1.Failing = true;

                // Verify initial state
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                // Send message (should fail as endpoint is dead)
                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                // Verify unhealthySince is set, but lastFailedRevivalTime is not (because it hasn't died yet)
                DateTime unhealthySince = status.UnhealthySince.GetOrElse(DateTime.MinValue);
                Assert.True(DateTime.Now - unhealthySince < TimeSpan.FromSeconds(0.5));
                Assert.False(status.LastFailedRevivalTime.HasValue);

                // Retry and fail once more
                await machine.RunAsync(Commands.Retry);
                status = machine.Status;

                // Verify unhealthySince hasn't changed, and lastFailedRevivalTime is still not populated
                DateTime unhealthySince2 = status.UnhealthySince.GetOrElse(DateTime.MinValue);
                Assert.Equal(unhealthySince, unhealthySince2);
                Assert.False(status.LastFailedRevivalTime.HasValue);

                // Enable the endpoint
                endpoint1.Failing = false;

                // Retry should succeed
                await machine.RunAsync(Commands.Retry);
                await command.Completion;
                status = machine.Status;
                Assert.Equal(State.Idle, status.State);

                // Verify unhealthySince and lastFailedRevivalTime have no value
                Assert.False(status.LastFailedRevivalTime.HasValue);
                Assert.False(status.UnhealthySince.HasValue);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestDie()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);

                await command.Completion;

                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(5, status.RetryAttempts);
                Assert.True(DateTime.Now - status.LastFailedRevivalTime.GetOrElse(DateTime.MinValue) < TimeSpan.FromSeconds(0.5));
                Assert.True(DateTime.Now - status.UnhealthySince.GetOrElse(DateTime.MinValue) < TimeSpan.FromSeconds(0.5));

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestDieToThrow()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5), true);

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);

                Exception exception = await Assert.ThrowsAsync<Exception>(() => command.Completion);
                Assert.Equal("endpoint failed", exception.Message);

                status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestRetryTimer()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(50));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                SendMessage command = Commands.SendMessage(Message1);
                await machine.RunAsync(command);
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await command.Completion;

                // TransitionAction happens before transition to next state and hence add a small delay
                await Task.Delay(TimeSpan.FromMilliseconds(1));
                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(5, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestReviveToSuccess()
        {
            var endpoint1 = new RevivableEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(int.MaxValue));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMilliseconds(50));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.RunAsync(Commands.SendMessage(Message2));
                status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);
                Assert.Equal(new List<IMessage> { Message2 }, endpoint1.Processed);

                // Fail the endpoint
                endpoint1.Failing = true;

                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);

                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(5, status.RetryAttempts);

                // Wait for the revive period to expire
                await Task.Delay(100);

                // Bring the endpoint back in the dead state
                endpoint1.Failing = false;
                await machine.RunAsync(Commands.SendMessage(Message1));

                status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);
                Assert.Equal(new List<IMessage> { Message2, Message1 }, endpoint1.Processed);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestReviveToFail()
        {
            var endpoint1 = new RevivableEndpoint("id1", new Exception("endpoint failed"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(int.MaxValue));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMilliseconds(50));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.RunAsync(Commands.SendMessage(Message2));
                status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);
                Assert.Equal(new List<IMessage> { Message2 }, endpoint1.Processed);

                // Fail the endpoint
                endpoint1.Failing = true;

                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);
                await machine.RunAsync(Commands.Retry);

                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(5, status.RetryAttempts);

                // Wait for the revive period to expire
                await Task.Delay(100);

                // Send another message, after the revive period but with a still failing endpoint
                await machine.RunAsync(Commands.SendMessage(Message1));

                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(5, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestToDeadOnNonTransient()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("nontransient"));
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(int.MaxValue));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMilliseconds(50));

            using (var machine = new EndpointExecutorFsm(endpoint1, new NullCheckpointer(), config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                // non transient exception
                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestDead()
        {
            var endpoint1 = new FailedEndpoint("id1", new Exception("endpoint failed"));
            var checkpointer = new Mock<ICheckpointer>();
            checkpointer.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            var machine = new EndpointExecutorFsm(endpoint1, checkpointer.Object, config);
            EndpointExecutorStatus status = machine.Status;
            Assert.Equal(State.Idle, status.State);
            Assert.Equal(0, status.RetryAttempts);

            await machine.RunAsync(Commands.SendMessage(Message1));
            status = machine.Status;
            Assert.Equal(State.Failing, status.State);
            Assert.Equal(1, status.RetryAttempts);

            await machine.RunAsync(Commands.Retry);
            await machine.RunAsync(Commands.Retry);
            await machine.RunAsync(Commands.Retry);
            await machine.RunAsync(Commands.Retry);

            // Test no checkpoint
            checkpointer.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Never);

            await machine.RunAsync(Commands.Retry);

            checkpointer.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));

            await machine.RunAsync(Commands.SendMessage(Message1));
            await machine.RunAsync(Commands.SendMessage(Message2));

            checkpointer.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

            await machine.RunAsync(Commands.SendMessage());
            checkpointer.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

            await machine.CloseAsync();
        }

        [Fact, Unit]
        public async Task TestDeadCheckpointException()
        {
            // Test operation canceled exception - no throw
            var endpoint1 = new FailedEndpoint("id2");
            var checkpointer1 = new Mock<ICheckpointer>();
            checkpointer1.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer1.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new OperationCanceledException());

            var retryStrategy = new FixedInterval(1, TimeSpan.FromMilliseconds(int.MaxValue));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine1 = new EndpointExecutorFsm(endpoint1, checkpointer1.Object, config))
            {
                await machine1.RunAsync(Commands.SendMessage(Message1));
                await machine1.RunAsync(Commands.Retry);
                Assert.Equal(State.DeadIdle, machine1.Status.State);
                checkpointer1.Verify(c => c.CommitAsync(new [] { Message1 }, It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
                await machine1.CloseAsync();
            }

            // Test exception throws
            var endpoint2 = new FailedEndpoint("id2");
            var checkpointer2 = new Mock<ICheckpointer>();
            checkpointer2.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Throws(new Exception());

            var machine2 = new EndpointExecutorFsm(endpoint2, checkpointer2.Object, config);
            SendMessage command2 = Commands.SendMessage(Message1);
            await machine2.RunAsync(command2);
            await machine2.RunAsync(Commands.Retry);
            await command2.Completion;
            Assert.Equal(State.DeadIdle, machine2.Status.State);
            checkpointer2.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));

            await machine2.RunAsync(Commands.SendMessage(Message1));
            checkpointer2.Verify(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            await machine2.CloseAsync();
        }

        [Fact, Unit]
        public async Task TestErrorDetectionStrategy()
        {
            var detectionStrategy = new ErrorDetectionStrategy(ex => ex.GetType() != typeof(InvalidOperationException));
            var endpoint1 = new FailedEndpoint("id1", "endpoint1", "hub1", new InvalidOperationException("endpoint failed"), detectionStrategy);
            var checkpointer = new Mock<ICheckpointer>();
            checkpointer.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<ICollection<IMessage>>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));

            using (var machine = new EndpointExecutorFsm(endpoint1, checkpointer.Object, config))
            {
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                var endpoint2 = new FailedEndpoint("id1", new Exception("endpoint failed"));
                await machine.RunAsync(Commands.UpdateEndpoint(endpoint2));
                status = machine.Status;
                Assert.Equal(State.Idle, status.State);
                Assert.Equal(0, status.RetryAttempts);

                await machine.RunAsync(Commands.SendMessage(Message1));
                status = machine.Status;
                Assert.Equal(State.Failing, status.State);
                Assert.Equal(1, status.RetryAttempts);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestDeadStatus()
        {
            var checkpointerStore = new Mock<ICheckpointStore>();
            DateTime dateTimeNow = DateTime.UtcNow;
            checkpointerStore.Setup(c => c.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(0L));
            checkpointerStore.Setup(c => c.SetCheckpointDataAsync(It.IsAny<string>(), new CheckpointData(It.IsAny<long>(), Option.Some(dateTimeNow), Option.None<DateTime>()), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            checkpointerStore.Setup(c => c.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CheckpointData(It.IsAny<long>(), Option.Some(dateTimeNow), Option.None<DateTime>())));

            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromSeconds(5));

            ICheckpointer checkpointer = await Checkpointer.CreateAsync("checkpoint.id1", checkpointerStore.Object);

            var endpoint = new TestEndpoint("endpoint1");

            using (var machine = new EndpointExecutorFsm(endpoint, checkpointer, config))
            {
                // TODO find a way to test this without a delay
                //await machine.RunAsync(Commands.SendMessage(Message1));
                //// checkpoint should have dropped and moved the offset
                //Assert.Equal(0, endpoint.N); // endpoint should not get the message as it is dead
                //EndpointExecutorStatus status = machine.Status;
                //Assert.Equal("endpoint1", status.Id);
                //Assert.Equal(short.MaxValue, status.RetryAttempts);
                //Assert.Equal(State.DeadIdle, status.State);
                //Assert.NotEqual(DateTime.UtcNow, status.LastFailedRevivalTime.GetOrElse(DateTime.MinValue));
                //Assert.True(DateTime.UtcNow > status.LastFailedRevivalTime.GetOrElse(DateTime.MinValue));
                //Assert.Equal("checkpoint.id1", status.CheckpointerStatus.Id);
                //Assert.Equal(1, status.CheckpointerStatus.Offset);

                //await Task.Delay(TimeSpan.FromSeconds(5));
                //await machine.RunAsync(Commands.SendMessage(Message2));

                // Check with revival now
                //Assert.Equal(1, endpoint.N); // endpoint gets the message with revival
                //status = machine.Status;
                //Assert.Equal("endpoint1", status.Id);
                //Assert.Equal(0, status.RetryAttempts);
                //Assert.Equal(State.Idle, status.State);
                //Assert.Equal(Checkpointer.DateTimeMinValue, status.LastFailedRevivalTime.GetOrElse(Checkpointer.DateTimeMinValue));
                //Assert.Equal("checkpoint.id1", status.CheckpointerStatus.Id);
                //Assert.Equal(2, status.CheckpointerStatus.Offset);

                await machine.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestReliableDeadStatus()
        {
            var checkpointerStore = new Mock<ICheckpointStore>();
            DateTime dateTimeNow = DateTime.UtcNow;
            checkpointerStore.Setup(c => c.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(0L, Option.Some(dateTimeNow), Option.None<DateTime>()));
            checkpointerStore.Setup(c => c.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CheckpointData(It.IsAny<long>(), Option.Some(dateTimeNow), Option.None<DateTime>())));

            ICheckpointer checkpointer = await Checkpointer.CreateAsync("checkpoint.id1", checkpointerStore.Object);
            var retryStrategy = new FixedInterval(5, TimeSpan.FromMilliseconds(5));
            var config = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, retryStrategy, TimeSpan.FromMinutes(5));
            var endpoint = new TestEndpoint("endpoint1");

            using (var machine = new EndpointExecutorFsm(endpoint, checkpointer, config))
            {
                await machine.RunAsync(Commands.SendMessage(Message1));
                Assert.Equal(0, endpoint.N); // endpoint should not get the message as it is dead
                EndpointExecutorStatus status = machine.Status;
                Assert.Equal("endpoint1", status.Id);
                Assert.Equal(short.MaxValue, status.RetryAttempts);
                Assert.Equal(State.DeadIdle, status.State);
                Assert.Equal(dateTimeNow, status.LastFailedRevivalTime.GetOrElse(DateTime.MinValue));
                Assert.Equal("checkpoint.id1", status.CheckpointerStatus.Id);
                Assert.Equal(1, status.CheckpointerStatus.Offset);
                await machine.CloseAsync();
            }

            // restart executor and still we should be in dead state
            using (var machine1 = new EndpointExecutorFsm(endpoint, checkpointer, config))
            {
                Assert.Equal(machine1.Status.LastFailedRevivalTime.GetOrElse(DateTime.MinValue), dateTimeNow);
                Assert.Equal(State.DeadIdle, machine1.Status.State);
                await machine1.CloseAsync();
            }
        }

        [Fact, Unit]
        public async Task TestInvalidMessages()
        {
            // Check that all successful and invalid messages are checkpointed
            Checkpointer checkpointer = await Checkpointer.CreateAsync("checkpointer", new NullCheckpointStore(0L));

            var result = new SinkResult<IMessage>(new List<IMessage> { Message2 }, new List<IMessage>(), new List<InvalidDetails<IMessage>> { new InvalidDetails<IMessage>(Message3, FailureKind.MaxMessageSizeExceeded), new InvalidDetails<IMessage>(Message1, FailureKind.MaxMessageSizeExceeded)}, new SendFailureDetails(FailureKind.InternalError, new Exception()));
            var processor = new Mock<IProcessor>();
            processor.Setup(p => p.ErrorDetectionStrategy).Returns(new ErrorDetectionStrategy(_ => true));
            processor.Setup(p => p.ProcessAsync(It.IsAny<ICollection<IMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            var endpoint = new InvalidEndpoint("id1", () => processor.Object);
            processor.Setup(p => p.Endpoint).Returns(endpoint);

            var machine = new EndpointExecutorFsm(endpoint, checkpointer, MaxConfig);
            Assert.Equal(State.Idle, machine.Status.State);
            await machine.RunAsync(Commands.SendMessage(Message1, Message2, Message3));

            Assert.Equal(State.Idle, machine.Status.State);
            Assert.Equal(3L, checkpointer.Offset);
        }

        class InvalidEndpoint : Endpoint
        {
            readonly Func<IProcessor> processorFactory;

            public override string Type => "InvalidEndpoint";

            public InvalidEndpoint(string id, Func<IProcessor> processorFactory)
                : base(id)
            {
                this.processorFactory = processorFactory;
            }

            public override IProcessor CreateProcessor() => this.processorFactory();

            public override void LogUserMetrics(long messageCount, long latencyInMs)
            {
            }
        }
    }
}