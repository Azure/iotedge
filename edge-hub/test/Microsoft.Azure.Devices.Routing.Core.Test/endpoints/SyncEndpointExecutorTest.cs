// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Moq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class SyncEndpointExecutorTest : RoutingUnitTestBase
    {
        static readonly IMessage Default = new Message(TelemetryMessageSource.Instance, new byte[0], new Dictionary<string, string>());
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 1);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 2);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 3);

        static readonly SyncEndpointExecutorFactory Factory = new SyncEndpointExecutorFactory(TestConstants.DefaultConfig);

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new SyncEndpointExecutor(null, null));
            Assert.Throws<ArgumentNullException>(() => new SyncEndpointExecutor(null, new NullCheckpointer()));
            Assert.Throws<ArgumentNullException>(() => new SyncEndpointExecutor(new TestEndpoint("id"), null));
        }

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint = new TestEndpoint("id");
            Checkpointer checkpointer = await Checkpointer.CreateAsync("exec1", NullCheckpointStore.Instance);
            var executor = new SyncEndpointExecutor(endpoint, checkpointer);
            var expected = new List<IMessage> { Message1, Message2, Message3 };

            Assert.Equal(State.Idle, executor.Status.State);
            Assert.Equal(Checkpointer.InvalidOffset, executor.Status.CheckpointerStatus.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, executor.Status.CheckpointerStatus.Proposed);

            foreach (IMessage msg in expected)
            {
                await executor.Invoke(msg);
            }

            await executor.CloseAsync();
            Assert.Equal(3, endpoint.N);
            Assert.Equal(expected, endpoint.Processed);
            Assert.Equal(3, executor.Status.CheckpointerStatus.Offset);
            Assert.Equal(3, executor.Status.CheckpointerStatus.Proposed);
        }

        [Fact]
        [Unit]
        public async Task TestCancellation()
        {
            var retryStrategy = new FixedInterval(10, TimeSpan.FromSeconds(1));
            TimeSpan revivePeriod = TimeSpan.FromHours(1);
            TimeSpan execTimeout = TimeSpan.FromSeconds(60);
            var config = new EndpointExecutorConfig(execTimeout, retryStrategy, revivePeriod, true);

            var endpoint = new FailedEndpoint("id");

            IProcessor processor = endpoint.CreateProcessor();
            Assert.Equal(endpoint, processor.Endpoint);

            IEndpointExecutor executor = new SyncEndpointExecutor(endpoint, new NullCheckpointer(), config);
            Task running = executor.Invoke(Default);
            await executor.CloseAsync();
            await running;
            Assert.True(running.IsCompleted);
        }

        [Fact]
        [Unit]
        public async Task TestClose()
        {
            var endpoint = new TestEndpoint("id");
            IEndpointExecutor executor = await Factory.CreateAsync(endpoint);
            Task running = executor.Invoke(Default);

            await executor.CloseAsync();
            await running;
            Assert.True(running.IsCompleted);

            // ensure this doesn't throw
            await executor.CloseAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.Invoke(Default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.SetEndpoint(endpoint));
        }

        [Fact]
        [Unit]
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id1");
            var endpoint3 = new TestEndpoint("id3");
            IEndpointExecutor executor = await Factory.CreateAsync(endpoint1);

            Assert.Equal(new List<IMessage>(), endpoint1.Processed);
            Assert.Equal(new List<IMessage>(), endpoint2.Processed);
            Assert.Equal(new List<IMessage>(), endpoint3.Processed);
            await Assert.ThrowsAsync<ArgumentException>(() => executor.SetEndpoint(endpoint3));

            await executor.Invoke(Message1);
            await executor.Invoke(Message1);

            Assert.Equal(new List<IMessage> { Message1, Message1 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage>(), endpoint2.Processed);
            Assert.Equal(new List<IMessage>(), endpoint3.Processed);

            await executor.SetEndpoint(endpoint2);
            Assert.Equal(new List<IMessage> { Message1, Message1 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage>(), endpoint2.Processed);
            Assert.Equal(new List<IMessage>(), endpoint3.Processed);

            await executor.Invoke(Message2);
            await executor.Invoke(Message3);

            Assert.Equal(new List<IMessage> { Message1, Message1 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { Message2, Message3 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage>(), endpoint3.Processed);

            await executor.CloseAsync();
        }

        [Fact]
        [Unit]
        public async Task TestCheckpoint()
        {
            var endpoint1 = new TestEndpoint("id1");
            var checkpointer = new Mock<ICheckpointer>();
            checkpointer.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(true);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<IMessage[]>(), It.IsAny<IMessage[]>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            checkpointer.Setup(c => c.CommitAsync(It.IsAny<IMessage[]>(), It.IsAny<IMessage[]>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            IEndpointExecutor executor1 = new SyncEndpointExecutor(endpoint1, checkpointer.Object);
            await executor1.Invoke(Message1);

            checkpointer.Verify(c => c.CommitAsync(new[] { Message1 }, new IMessage[0], It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Exactly(1));

            var endpoint2 = new NullEndpoint("id2");
            var checkpointer2 = new Mock<ICheckpointer>();
            checkpointer2.Setup(c => c.Admit(It.IsAny<IMessage>())).Returns(false);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<IMessage[]>(), It.IsAny<IMessage[]>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            checkpointer2.Setup(c => c.CommitAsync(It.IsAny<IMessage[]>(), It.IsAny<IMessage[]>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);

            IEndpointExecutor executor2 = new SyncEndpointExecutor(endpoint2, checkpointer2.Object);
            await executor2.Invoke(Message1);

            checkpointer2.Verify(c => c.CommitAsync(It.IsAny<IMessage[]>(), It.IsAny<IMessage[]>(), It.IsAny<Option<DateTime>>(), It.IsAny<Option<DateTime>>(), It.IsAny<CancellationToken>()), Times.Never);

            await executor1.CloseAsync();
            await executor2.CloseAsync();
        }

        [Fact]
        [Unit]
        public async Task TestProcessorFailure()
        {
            var endpoint1 = new FailedEndpoint("id1", new OperationCanceledException());
            var endpoint2 = new FailedEndpoint("id1", new InvalidOperationException());

            var executor1 = new SyncEndpointExecutor(endpoint1, new NullCheckpointer());
            await Assert.ThrowsAsync<OperationCanceledException>(() => executor1.Invoke(Message1));

            var executor2 = new SyncEndpointExecutor(endpoint2, new NullCheckpointer());
            await Assert.ThrowsAsync<InvalidOperationException>(() => executor2.Invoke(Message1));
        }
    }
}
