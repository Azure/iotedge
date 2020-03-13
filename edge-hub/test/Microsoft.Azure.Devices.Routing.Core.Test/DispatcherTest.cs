// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Moq;
    using Xunit;

    public class DispatcherTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 0L);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 1L);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 2L);

        static readonly IEndpointExecutorFactory AsyncExecutorFactory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);
        static readonly IEndpointExecutorFactory SyncExecutorFactory = new SyncEndpointExecutorFactory(TestConstants.DefaultConfig);

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 0 } }, { endpoint2, new List<uint>() { 0 } } };
            var message = new Message(TelemetryMessageSource.Instance, new byte[0], new Dictionary<string, string>());

            Assert.Equal(new List<IMessage>(), endpoint1.Processed);
            Assert.Equal(new List<IMessage>(), endpoint2.Processed);

            using (Dispatcher dispatcher = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, SyncExecutorFactory))
            {
                await dispatcher.DispatchAsync(message, new HashSet<RouteResult> { new RouteResult(endpoint1, 0, 3600) });
                await dispatcher.CloseAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(message, new HashSet<RouteResult> { new RouteResult(endpoint1, 0, 3600) }));

                // Ensure a second close doesn't throw
                await dispatcher.CloseAsync(CancellationToken.None);
            }

            var expected = new List<IMessage> { message };
            Assert.Equal(expected, endpoint1.Processed);
            Assert.Equal(new List<IMessage>(), endpoint2.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestConstructor()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => Dispatcher.CreateAsync(null, null, null, null));
        }

        [Fact]
        [Unit]
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id1");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 0 } }, { endpoint3, new List<uint>() { 0 } } };
            using (Dispatcher dispatcher = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, SyncExecutorFactory))
            {
                Assert.Equal(new List<IMessage>(), endpoint1.Processed);
                Assert.Equal(new List<IMessage>(), endpoint3.Processed);

                await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.SetEndpoint(null, new List<uint>() { 0 }));
                await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.SetEndpoint(endpoint1, null));

                RouteResult result1 = new RouteResult(endpoint1, 0, 3600);
                RouteResult result3 = new RouteResult(endpoint3, 0, 3600);
                RouteResult result4 = new RouteResult(endpoint4, 0, 3600);

                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3 });
                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3 });

                await dispatcher.SetEndpoint(endpoint2, new List<uint>() { 1, 2 });

                await dispatcher.DispatchAsync(Message2, new HashSet<RouteResult> { result1, result3 });
                await dispatcher.DispatchAsync(Message3, new HashSet<RouteResult> { result1, result3 });

                await dispatcher.SetEndpoint(endpoint4, new List<uint>() { 3, 4 });
                await dispatcher.DispatchAsync(Message2, new HashSet<RouteResult> { result1, result3, result4 });
                await dispatcher.DispatchAsync(Message3, new HashSet<RouteResult> { result1, result3, result4 });

                await dispatcher.CloseAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.SetEndpoint(endpoint2, new List<uint>() { 0 }));
            }

            var expected = new List<IMessage> { Message1, Message1, Message2, Message3, Message2, Message3 };
            Assert.Equal(expected, endpoint1.Processed.Concat(endpoint2.Processed));
            Assert.Equal(expected, endpoint3.Processed);
            Assert.Equal(new List<IMessage> { Message2, Message3 }, endpoint4.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestRemoveEndpoint()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 0 } }, { endpoint3, new List<uint>() { 0 } } };
            using (Dispatcher dispatcher = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, SyncExecutorFactory))
            {
                Assert.Equal(new List<IMessage>(), endpoint1.Processed);
                Assert.Equal(new List<IMessage>(), endpoint3.Processed);

                await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.RemoveEndpoint(null));

                RouteResult result1 = new RouteResult(endpoint1, 0, 3600);
                RouteResult result3 = new RouteResult(endpoint3, 0, 3600);
                RouteResult result4 = new RouteResult(endpoint4, 0, 3600);

                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3 });
                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3 });

                await dispatcher.RemoveEndpoint(endpoint1.Id);
                Assert.Equal(new[] { endpoint3 }, dispatcher.Endpoints);

                // Remove it again
                await dispatcher.RemoveEndpoint(endpoint1.Id);
                Assert.Equal(new[] { endpoint3 }, dispatcher.Endpoints);

                await dispatcher.DispatchAsync(Message2, new HashSet<RouteResult> { result1, result3 });
                await dispatcher.DispatchAsync(Message3, new HashSet<RouteResult> { result1, result3 });

                await dispatcher.SetEndpoint(endpoint4, new List<uint>() { 0 });
                await dispatcher.DispatchAsync(Message2, new HashSet<RouteResult> { result1, result3, result4 });
                await dispatcher.DispatchAsync(Message3, new HashSet<RouteResult> { result1, result3, result4 });

                await dispatcher.CloseAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.RemoveEndpoint(endpoint1.Id));
            }

            var expected = new List<IMessage> { Message1, Message1, Message2, Message3, Message2, Message3 };
            Assert.Equal(new List<IMessage> { Message1, Message1 }, endpoint1.Processed);
            Assert.Equal(expected, endpoint3.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestReplaceEndpoints()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 0 } }, { endpoint3, new List<uint>() { 0 } } };
            using (Dispatcher dispatcher = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, SyncExecutorFactory))
            {
                Assert.Equal(new List<IMessage>(), endpoint1.Processed);
                Assert.Equal(new List<IMessage>(), endpoint3.Processed);

                await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.ReplaceEndpoints(null));

                RouteResult result1 = new RouteResult(endpoint1, 0, 3600);
                RouteResult result3 = new RouteResult(endpoint3, 0, 3600);
                RouteResult result4 = new RouteResult(endpoint4, 0, 3600);

                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3, result4 });
                await dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { result1, result3, result4 });

                var newEndpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 1, 2 } }, { endpoint4, new List<uint>() { 3, 4 } } };
                await dispatcher.ReplaceEndpoints(newEndpoints);

                await dispatcher.DispatchAsync(Message2, new HashSet<RouteResult> { result1, result3, result4 });
                await dispatcher.DispatchAsync(Message3, new HashSet<RouteResult> { result1, result3, result4 });

                await dispatcher.CloseAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.ReplaceEndpoints(newEndpoints));
            }

            var expected1 = new List<IMessage> { Message1, Message1, Message2, Message3 };
            var expected3 = new List<IMessage> { Message1, Message1 };
            var expected4 = new List<IMessage> { Message2, Message3 };
            Assert.Equal(expected1, endpoint1.Processed);
            Assert.Equal(expected3, endpoint3.Processed);
            Assert.Equal(expected4, endpoint4.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestFailedEndpoint()
        {
            var retryStrategy = new FixedInterval(10, TimeSpan.FromSeconds(1));
            TimeSpan revivePeriod = TimeSpan.FromHours(1);
            TimeSpan execTimeout = TimeSpan.FromSeconds(60);
            var config = new EndpointExecutorConfig(execTimeout, retryStrategy, revivePeriod, true);
            var factory = new SyncEndpointExecutorFactory(config);

            var endpoint = new FailedEndpoint("endpoint1");
            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint, new List<uint>() { 0 } } };

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                Dispatcher dispatcher = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, factory);
                // Because the buffer size is one and we are failing we should block on the dispatch
                Task dispatching = dispatcher.DispatchAsync(Message1, new HashSet<RouteResult> { new RouteResult(endpoint, 0, 3600) });

                var endpoint2 = new TestEndpoint("endpoint1");
                Task setting = dispatcher.SetEndpoint(endpoint2, new List<uint>() { 0 });

                Task timeout = Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                await Task.WhenAny(dispatching, setting, timeout);
                await dispatcher.CloseAsync(CancellationToken.None);
                var expected = new List<IMessage> { Message1 };
                Assert.Equal(expected, endpoint2.Processed);
            }
        }

        [Fact]
        [Unit]
        public async Task TestClosedEndpoint()
        {
            var factory1 = new ClosedEndpointExecutorFactory(AsyncExecutorFactory);
            var endpoint1 = new TestEndpoint("endpoint1");
            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint1, new List<uint>() { 0 } } };

            Dispatcher dispatcher1 = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, factory1);

            // test doesn't throw on closed endpoint
            await dispatcher1.DispatchAsync(Message1, new HashSet<RouteResult> { new RouteResult(endpoint1, 0, 3600) });
            await dispatcher1.CloseAsync(CancellationToken.None);
        }

        [Fact]
        [Unit]
        public async Task TestShow()
        {
            var endpoint = new TestEndpoint("endpoint1");
            var endpoints = new Dictionary<Endpoint, IList<uint>> { { endpoint, new List<uint>() { 0 } } };

            Dispatcher dispatcher1 = await Dispatcher.CreateAsync("dispatcher", "hub", endpoints, AsyncExecutorFactory);
            Assert.Equal("Dispatcher(dispatcher)", dispatcher1.ToString());
            await dispatcher1.CloseAsync(CancellationToken.None);
        }

        [Fact]
        [Unit]
        public async Task TestOffset()
        {
            const uint DefaultPriority = Microsoft.Azure.Devices.Routing.Core.RouteFactory.DefaultPriority;
            var store = new Mock<ICheckpointStore>();
            var dispatcherId = Guid.NewGuid().ToString();
            string endpointId1 = "endpoint1";
            string endpointId2 = "endpoint2";
            store.Setup(s => s.GetCheckpointDataAsync(dispatcherId, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(21));
            store.Setup(s => s.GetCheckpointDataAsync(endpointId1, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(23));
            store.Setup(s => s.GetCheckpointDataAsync(endpointId2, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(22));

            var endpoint1 = new TestEndpoint(endpointId1);
            var endpoint2 = new TestEndpoint(endpointId2);
            var endpoints = new Dictionary<Endpoint, IList<uint>>
            {
                { endpoint1, new List<uint>() { DefaultPriority } },
                { endpoint2, new List<uint>() { DefaultPriority } }
            };
            Dispatcher dispatcher1 = await Dispatcher.CreateAsync(dispatcherId, "hub", endpoints, SyncExecutorFactory, store.Object);

            Assert.Equal(Option.Some(21L), dispatcher1.Offset);

            RouteResult result1 = new RouteResult(endpoint1, 0, 3600);
            RouteResult result2 = new RouteResult(endpoint2, 0, 3600);

            await dispatcher1.DispatchAsync(MessageWithOffset(24L), new HashSet<RouteResult> { result1 });
            Assert.Equal(Option.Some(24L), dispatcher1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync(dispatcherId, It.Is<CheckpointData>(c => c.Offset == 24), It.IsAny<CancellationToken>()), Times.Once());
            store.Verify(s => s.SetCheckpointDataAsync(endpointId1, It.Is<CheckpointData>(c => c.Offset == 24), It.IsAny<CancellationToken>()), Times.Once());

            await dispatcher1.DispatchAsync(MessageWithOffset(25L), new HashSet<RouteResult> { result1, result2 });
            Assert.Equal(Option.Some(25L), dispatcher1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync(dispatcherId, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once());
            store.Verify(s => s.SetCheckpointDataAsync(endpointId1, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once());
            store.Verify(s => s.SetCheckpointDataAsync(endpointId2, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once());

            await dispatcher1.DispatchAsync(MessageWithOffset(26L), new HashSet<RouteResult> { result2 });
            Assert.Equal(Option.Some(26L), dispatcher1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync(dispatcherId, It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once());
            store.Verify(s => s.SetCheckpointDataAsync(endpointId2, It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once());

            await dispatcher1.CloseAsync(CancellationToken.None);

            Dispatcher dispatcher2 = await Dispatcher.CreateAsync(Guid.NewGuid().ToString(), "hub", new Dictionary<Endpoint, IList<uint>>(), SyncExecutorFactory);
            Assert.Equal(Option.None<long>(), dispatcher2.Offset);
            await dispatcher2.CloseAsync(CancellationToken.None);
        }

        static IMessage MessageWithOffset(long offset) =>
            new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, offset);
    }
}
