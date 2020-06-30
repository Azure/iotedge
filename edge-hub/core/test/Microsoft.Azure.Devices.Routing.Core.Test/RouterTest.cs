// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
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

    [ExcludeFromCodeCoverage]
    public class RouterTest : RoutingUnitTestBase
    {
        static readonly Option<Route> Fallback = Option.None<Route>();
        static readonly IEndpointExecutorFactory AsyncExecutorFactory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);
        static readonly IEndpointExecutorFactory SyncExecutorFactory = new SyncEndpointExecutorFactory(TestConstants.DefaultConfig);

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
            var endpoint1 = new TestEndpoint("id1");
            var allEndpoints = new HashSet<Endpoint> { endpoint1 };
            var route = new Route("id", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route };

            using (Router router = await Router.CreateAsync("router1", "SmokeTest", new RouterConfig(allEndpoints, routes, Fallback), new SyncEndpointExecutorFactory(TestConstants.DefaultConfig)))
            {
                await router.RouteAsync(message1);
                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.RouteAsync(message1));
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.RouteAsync(new[] { message1 }));
            }

            var expected = new List<IMessage> { message1 };
            Assert.Equal(expected, endpoint1.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestClose()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route };

            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), AsyncExecutorFactory))
            {
                await router.CloseAsync(CancellationToken.None);

                // Ensure a second close doesn't throw
                await router.CloseAsync(CancellationToken.None);
            }
        }

        [Fact]
        [Unit]
        public async Task TestShow()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id1", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route };
            Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), AsyncExecutorFactory);
            string expected = "Router(router1)";
            Assert.Equal(expected, router.ToString());
            await router.CloseAsync(CancellationToken.None);
        }

        [Fact]
        [Unit]
        public async Task TestSetRoute()
        {
            var message1 = new Message(
                TelemetryMessageSource.Instance,
                new byte[] { 1, 2, 3 },
                new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue1" }, { "systemkey2", "systemvalue2" } });

            var message2 = new Message(
                TelemetryMessageSource.Instance,
                new byte[] { 2, 3, 1 },
                new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue2" }, { "systemkey2", "systemvalue2" } });

            var message3 = new Message(
                TelemetryMessageSource.Instance,
                new byte[] { 3, 1, 2 },
                new Dictionary<string, string> { { "key1", "value3" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue3" }, { "systemkey2", "systemvalue2" } });

            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoint3 = new TestEndpoint("id3");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3 };

            var route1 = new Route("id1", "key1 = \"value1\" and $systemkey1 = \"systemvalue1\"", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "key1 = \"value2\" and $systemkey1 = \"systemvalue2\"", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "key1 = \"value3\" and $systemkey1 = \"systemvalue3\"", "hub", TelemetryMessageSource.Instance, endpoint3, 0, 0);
            var route4 = new Route("id1", "key1 = \"value3\" and $systemkey1 = \"systemvalue3\"", "hub", TelemetryMessageSource.Instance, endpoint3, 5, 0);

            var routes = new HashSet<Route> { route1, route2, route3 };
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), new SyncEndpointExecutorFactory(TestConstants.DefaultConfig)))
            {
                Assert.Contains(route1, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Equal(3, router.Routes.Count);

                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.SetRoute(route4);

                Assert.Contains(route2, router.Routes);
                Assert.Contains(route3, router.Routes);
                Assert.Contains(route4, router.Routes);
                Assert.Equal(3, router.Routes.Count);
                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.SetRoute(route4));
            }

            Assert.Equal(new List<IMessage> { message1 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { message2, message2 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message3, message3 }, endpoint3.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestRemoveRoute()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
            var message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } });
            var message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

            var endpoint1 = new TestEndpoint("endpoint1");
            var endpoint2 = new TestEndpoint("endpoint2");
            var endpoint3 = new TestEndpoint("endpoint3");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3 };

            var route1 = new Route("id1", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "key1 = \"value2\"", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint3, 0, 0);

            var routes = new HashSet<Route> { route1, route2, route3 };
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), SyncExecutorFactory))
            {
                Assert.Contains(route1, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Contains(route3, router.Routes);
                Assert.Equal(3, router.Routes.Count);

                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.RemoveRoute("id1");
                Assert.Contains(route3, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Equal(2, router.Routes.Count);
                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.RemoveRoute("id3");
                Assert.Contains(route2, router.Routes);
                Assert.Equal(1, router.Routes.Count);
                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.RemoveRoute("id2"));
            }

            Assert.Equal(new List<IMessage> { message1, message3 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { message2, message2, message2 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message1, message3, message1, message3 }, endpoint3.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestReplaceRoutes()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 1, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 0L);
            var message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 2, 2 }, new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } }, 1L);
            var message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 3, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 2L);
            var message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 4, 4, 4 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 3L);
            var message5 = new Message(TelemetryMessageSource.Instance, new byte[] { 5, 5, 5 }, new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } }, 4L);
            var message6 = new Message(TelemetryMessageSource.Instance, new byte[] { 6, 6, 6 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 5L);
            var message7 = new Message(TelemetryMessageSource.Instance, new byte[] { 7, 7, 7 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 6L);
            var message8 = new Message(TelemetryMessageSource.Instance, new byte[] { 8, 8, 8 }, new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } }, 7L);
            var message9 = new Message(TelemetryMessageSource.Instance, new byte[] { 9, 9, 9 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 8L);

            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoint3 = new TestEndpoint("id3");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3 };

            var route1 = new Route("id1", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "key1 = \"value2\"", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint3, 0, 0);
            var route4 = new Route("id4", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint3, 1, 0);
            var route5 = new Route("id5", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, endpoint3, 2, 0);

            var routes = new HashSet<Route> { route1, route2 };
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), SyncExecutorFactory))
            {
                Assert.Contains(route1, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Equal(2, router.Routes.Count);

                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.ReplaceRoutes(new HashSet<Route> { route1, route2, route3 });
                Assert.Contains(route3, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Contains(route1, router.Routes);
                Assert.Equal(3, router.Routes.Count);

                await router.RouteAsync(message4);
                await router.RouteAsync(message5);
                await router.RouteAsync(message6);

                await router.ReplaceRoutes(new HashSet<Route> { route2, route3 });
                Assert.Contains(route2, router.Routes);
                Assert.Contains(route3, router.Routes);
                Assert.Equal(2, router.Routes.Count);
                await router.RouteAsync(message7);
                await router.RouteAsync(message8);
                await router.RouteAsync(message9);

                await router.ReplaceRoutes(new HashSet<Route> { route2, route3, route4, route5 });
                Assert.Contains(route2, router.Routes);
                Assert.Contains(route3, router.Routes);
                Assert.Contains(route4, router.Routes);
                Assert.Contains(route5, router.Routes);
                Assert.Equal(4, router.Routes.Count);

                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.ReplaceRoutes(new HashSet<Route>()));
            }

            Assert.Equal(new List<IMessage> { message1, message3, message4, message6 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { message2, message5, message8 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message4, message6, message7, message9 }, endpoint3.Processed);
        }

        [Fact]
        [Unit]
        public async Task TestFailedEndpoint()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
            var retryStrategy = new FixedInterval(10, TimeSpan.FromSeconds(1));
            TimeSpan revivePeriod = TimeSpan.FromHours(1);
            TimeSpan execTimeout = TimeSpan.FromSeconds(60);
            var config = new EndpointExecutorConfig(execTimeout, retryStrategy, revivePeriod, true);
            var factory = new SyncEndpointExecutorFactory(config);

            var endpoint = new FailedEndpoint("endpoint1");
            var endpoints = new HashSet<Endpoint> { endpoint };
            var route = new Route("route1", "true", "hub", TelemetryMessageSource.Instance, endpoint, 0, 0);

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(endpoints, new HashSet<Route> { route }, Fallback), factory))
            {
                // Because the buffer size is one and we are failing we should block on the dispatch
                Task routing = router.RouteAsync(message1);

                var endpoint2 = new TestEndpoint("endpoint1");
                var newRoute = new Route("id", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
                Task setting = router.SetRoute(newRoute);

                Task timeout = Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                await Task.WhenAny(routing, setting, timeout);
                await router.CloseAsync(CancellationToken.None);
                var expected = new List<IMessage> { message1 };
                Assert.Equal(expected, endpoint2.Processed);
            }
        }

        [Fact]
        [Unit]
        public async Task TestOffset()
        {
            var dispatcherId = Guid.NewGuid().ToString();
            string endpointId1 = "endpoint1";
            string endpointId2 = "endpoint2";
            var store = new Mock<ICheckpointStore>();
            store.Setup(s => s.GetCheckpointDataAsync(dispatcherId, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(22));
            store.Setup(s => s.GetCheckpointDataAsync(endpointId1, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(23));
            store.Setup(s => s.GetCheckpointDataAsync(endpointId2, It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(22));

            var endpoint1 = new TestEndpoint("endpoint1");
            var endpoint2 = new TestEndpoint("endpoint2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };

            var routes = new HashSet<Route>
            {
                new Route("route1", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0),
                new Route("route2", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0)
            };

            Router router1 = await Router.CreateAsync(dispatcherId, "hub", new RouterConfig(allEndpoints, routes, Fallback), SyncExecutorFactory, store.Object);
            Assert.Equal(Option.Some(22L), router1.Offset);

            store.Verify(s => s.GetCheckpointDataAsync(dispatcherId, It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.GetCheckpointDataAsync(endpointId1, It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.GetCheckpointDataAsync(endpointId2, It.IsAny<CancellationToken>()), Times.Once);

            await router1.RouteAsync(MessageWithOffset(25L));
            Assert.Equal(Option.Some(25L), router1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync(dispatcherId, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync(endpointId1, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync(endpointId2, It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);

            await router1.RemoveRoute("route1");
            await router1.RouteAsync(MessageWithOffset(26L));
            Assert.Equal(Option.Some(26L), router1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync(dispatcherId, It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync(endpointId1, It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Never);
            store.Verify(s => s.SetCheckpointDataAsync(endpointId2, It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once);

            await router1.CloseAsync(CancellationToken.None);

            Router router2 = await Router.CreateAsync("router2", "hub", new RouterConfig(allEndpoints, new HashSet<Route>(), Fallback), SyncExecutorFactory);
            Assert.Equal(Option.None<long>(), router2.Offset);
            await router2.CloseAsync(CancellationToken.None);
        }

        [Fact]
        [Unit]
        public async Task TestFallback()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id", "false", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var fallback = new Route("$fallback", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var routes = new HashSet<Route> { route };

            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Option.Some(fallback)), SyncExecutorFactory))
            {
                await router.RouteAsync(message1);
                await router.CloseAsync(CancellationToken.None);
            }

            var expected = new List<IMessage> { message1 };
            Assert.Equal(new List<IMessage>(), endpoint1.Processed);
            Assert.Equal(expected, endpoint2.Processed);
        }

        static IMessage MessageWithOffset(long offset) =>
            new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, offset);
    }
}
