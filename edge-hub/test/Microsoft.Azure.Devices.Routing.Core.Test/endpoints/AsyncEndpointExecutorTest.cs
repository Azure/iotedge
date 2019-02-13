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
    public class AsyncEndpointExecutorTest : RoutingUnitTestBase
    {
        static readonly IMessage Default = new Message(TelemetryMessageSource.Instance, new byte[0], new Dictionary<string, string>());
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 1);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 2);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 3);

        static readonly RetryStrategy MaxRetryStrategy = new FixedInterval(int.MaxValue, TimeSpan.FromMilliseconds(int.MaxValue));
        static readonly EndpointExecutorConfig MaxConfig = new EndpointExecutorConfig(Timeout.InfiniteTimeSpan, MaxRetryStrategy, TimeSpan.FromMinutes(5));

        static readonly AsyncEndpointExecutorFactory Factory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new AsyncEndpointExecutor(null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new AsyncEndpointExecutor(null, new NullCheckpointer(), null, null));
            Assert.Throws<ArgumentNullException>(() => new AsyncEndpointExecutor(new TestEndpoint("id"), null, null, null));
        }

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint = new TestEndpoint("id");
            Checkpointer checkpointer = await Checkpointer.CreateAsync("exec1", NullCheckpointStore.Instance);
            var executor = new AsyncEndpointExecutor(endpoint, checkpointer, MaxConfig, new AsyncEndpointExecutorOptions(1));
            var expected = new List<IMessage> { Message1, Message2, Message3 };

            Assert.Equal(State.Idle, executor.Status.State);
            Assert.Equal(Checkpointer.InvalidOffset, executor.Status.CheckpointerStatus.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, executor.Status.CheckpointerStatus.Proposed);

            foreach (IMessage msg in expected)
            {
                await executor.Invoke(msg);
            }

            await Task.Delay(30);
            await executor.CloseAsync();
            Assert.Equal(3, endpoint.N);
            Assert.Equal(expected, endpoint.Processed);
            Assert.Equal(3, executor.Status.CheckpointerStatus.Offset);
            Assert.Equal(3, executor.Status.CheckpointerStatus.Proposed);
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
        }

        [Fact]
        [Unit]
        public async Task TestCancellation()
        {
            var endpoint = new StalledEndpoint("id");
            IEndpointExecutor executor = await Factory.CreateAsync(endpoint);
            Task running = executor.Invoke(Default);

            await executor.CloseAsync();
            await running;
            Assert.True(running.IsCompleted);
        }

        [Fact]
        [Unit]
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id");
            var endpoint2 = new NullEndpoint("id");
            var endpoint3 = new TestEndpoint("id1");
            IEndpointExecutor executor = await Factory.CreateAsync(endpoint1);

            Assert.Equal(endpoint1, executor.Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => executor.SetEndpoint(null));
            await Assert.ThrowsAsync<ArgumentException>(() => executor.SetEndpoint(endpoint3));

            await executor.SetEndpoint(endpoint2);
            Assert.Equal(endpoint2, executor.Endpoint);

            await executor.CloseAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.SetEndpoint(endpoint1));
        }

        [Fact]
        [Unit]
        public async Task TestBatchTimeout()
        {
            var endpoint1 = new TestEndpoint("id1");
            var executor = new AsyncEndpointExecutor(endpoint1, new NullCheckpointer(), MaxConfig, new AsyncEndpointExecutorOptions(100, TimeSpan.FromMilliseconds(50)));

            await executor.Invoke(Message1);
            await executor.Invoke(Message1);

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            var expected = new List<IMessage> { Message1, Message1 };
            Assert.Equal(expected, endpoint1.Processed);

            await executor.Invoke(Message2);
            expected.Add(Message2);

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.Equal(expected, endpoint1.Processed);

            await executor.Invoke(Message2);
            expected.Add(Message2);

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.Equal(expected, endpoint1.Processed);

            await executor.CloseAsync();
        }

        [Fact]
        [Unit]
        public async Task TestStatus()
        {
            var checkpointerStore = new Mock<ICheckpointStore>();
            checkpointerStore.Setup(c => c.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(0L));
            checkpointerStore.Setup(c => c.SetCheckpointDataAsync(It.IsAny<string>(), new CheckpointData(It.IsAny<long>()), It.IsAny<CancellationToken>())).Returns(TaskEx.Done);
            ICheckpointer checkpointer = await Checkpointer.CreateAsync("checkpoint.id1", checkpointerStore.Object);

            var endpoint = new TestEndpoint("endpoint1");

            using (var executor = new AsyncEndpointExecutor(endpoint, checkpointer, MaxConfig, new AsyncEndpointExecutorOptions(1)))
            {
                await executor.Invoke(Message1);
                await Task.Delay(20);
                await executor.CloseAsync();

                Assert.Equal(1, endpoint.N);
                EndpointExecutorStatus status = executor.Status;
                Assert.Equal("endpoint1", status.Id);
                Assert.Equal(0, status.RetryAttempts);
                Assert.Equal(State.Closed, status.State);
                Assert.Equal("checkpoint.id1", status.CheckpointerStatus.Id);
                Assert.Equal(1, status.CheckpointerStatus.Offset);
            }
        }
    }
}
