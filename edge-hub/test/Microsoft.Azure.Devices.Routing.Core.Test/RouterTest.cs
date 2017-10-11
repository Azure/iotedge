// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
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
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class RouterTest : RoutingUnitTestBase
    {
        static IMessage MessageWithOffset(long offset) =>
             new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} }, offset);

        static readonly Option<Route> Fallback = Option.None<Route>();
        static readonly IEndpointExecutorFactory AsyncExecutorFactory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);
        static readonly IEndpointExecutorFactory SyncExecutorFactory = new SyncEndpointExecutorFactory(TestConstants.DefaultConfig);

        [Fact, Unit]
        public async Task SmokeTest()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
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
            Assert.Equal(expected, endpoint2.Processed);
        }

        [Fact, Unit]
        public async Task TestClose()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
            var routes = new HashSet<Route> { route };

            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), AsyncExecutorFactory))
            {
                await router.CloseAsync(CancellationToken.None);

                // Ensure a second close doesn't throw
                await router.CloseAsync(CancellationToken.None);
            }
        }

        [Fact, Unit]
        public async Task TestShow()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id1", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
            var routes = new HashSet<Route> { route };
            Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), AsyncExecutorFactory);
            string expected = "Router(router1)";
            Assert.Equal(expected, router.ToString());
            await router.CloseAsync(CancellationToken.None);
        }

        [Fact, Unit]
        public async Task TestSetRoute()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 },
                new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue1" }, { "systemkey2", "systemvalue2" } });

            var message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 },
                new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue2" }, { "systemkey2", "systemvalue2" } });

            var message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 },
                new Dictionary<string, string> { { "key1", "value3" }, { "key2", "value2" } },
                new Dictionary<string, string> { { "systemkey1", "systemvalue3" }, { "systemkey2", "systemvalue2" } });

            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3, endpoint4 };

            var route1 = new Route("id1", "key1 = \"value1\" and $systemkey1 = \"systemvalue1\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
            var route2 = new Route("id2", "key1 = \"value2\" and $systemkey1 = \"systemvalue2\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });
            var route3 = new Route("id1", "key1 = \"value3\" and $systemkey1 = \"systemvalue3\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });

            var routes = new HashSet<Route> { route1, route2 };
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(allEndpoints, routes, Fallback), new SyncEndpointExecutorFactory(TestConstants.DefaultConfig)))
            {
                Assert.Contains(route1, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Equal(2, router.Routes.Count);

                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.SetRoute(route3);

                Assert.Contains(route3, router.Routes);
                Assert.Contains(route2, router.Routes);
                Assert.Equal(2, router.Routes.Count);
                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.SetRoute(route3));
            }

            Assert.Equal(new List<IMessage> { message1 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { message1 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message2, message2, message3 }, endpoint3.Processed);
            Assert.Equal(new List<IMessage> { message2, message2, message3 }, endpoint4.Processed);
        }

        [Fact, Unit]
        public async Task TestRemoveRoute()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
            var message2 = new Message(TelemetryMessageSource.Instance, new byte[] {2, 3, 1}, new Dictionary<string, string> { {"key1", "value2"}, {"key2", "value2"} });
            var message3 = new Message(TelemetryMessageSource.Instance, new byte[] {3, 1, 2}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });

            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3, endpoint4 };

            var route1 = new Route("id1", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
            var route2 = new Route("id2", "key1 = \"value2\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });
            var route3 = new Route("id3", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });

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
            Assert.Equal(new List<IMessage> { message1, message3 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message1, message2, message3, message1, message2, message3, message2 }, endpoint3.Processed);
            Assert.Equal(new List<IMessage> { message1, message2, message3, message1, message2, message3, message2 }, endpoint4.Processed);
        }

        [Fact, Unit]
        public async Task TestReplaceRoutes()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
            var message2 = new Message(TelemetryMessageSource.Instance, new byte[] {2, 3, 1}, new Dictionary<string, string> { {"key1", "value2"}, {"key2", "value2"} });
            var message3 = new Message(TelemetryMessageSource.Instance, new byte[] {3, 1, 2}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });

            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var endpoint3 = new TestEndpoint("id3");
            var endpoint4 = new TestEndpoint("id4");

            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2, endpoint3, endpoint4 };

            var route1 = new Route("id1", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1, endpoint2 });
            var route2 = new Route("id2", "key1 = \"value2\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });
            var route3 = new Route("id3", "key1 = \"value1\"", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3, endpoint4 });

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

                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.ReplaceRoutes(new HashSet<Route> { route2, route3 });
                Assert.Contains(route2, router.Routes);
                Assert.Contains(route3, router.Routes);
                Assert.Equal(2, router.Routes.Count);
                await router.RouteAsync(message1);
                await router.RouteAsync(message2);
                await router.RouteAsync(message3);

                await router.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => router.ReplaceRoutes(new HashSet<Route>()));
            }

            Assert.Equal(new List<IMessage> { message1, message3, message1, message3 }, endpoint1.Processed);
            Assert.Equal(new List<IMessage> { message1, message3, message1, message3 }, endpoint2.Processed);
            Assert.Equal(new List<IMessage> { message2, message1, message2, message3, message1, message2, message3 }, endpoint3.Processed);
            Assert.Equal(new List<IMessage> { message2, message1, message2, message3, message1, message2, message3 }, endpoint4.Processed);
        }

        [Fact, Unit]
        public async Task TestFailedEndpoint()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
            var retryStrategy = new FixedInterval(10, TimeSpan.FromSeconds(1));
            TimeSpan revivePeriod = TimeSpan.FromHours(1);
            TimeSpan execTimeout = TimeSpan.FromSeconds(60);
            var config = new EndpointExecutorConfig(execTimeout, retryStrategy, revivePeriod, true);
            var factory = new SyncEndpointExecutorFactory(config);

            var endpoint = new FailedEndpoint("endpoint1");
            var endpoints = new HashSet<Endpoint> { endpoint };
            var route = new Route("route1", "true", "hub", TelemetryMessageSource.Instance, endpoints);

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            using (Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(endpoints, new HashSet<Route> { route }, Fallback), factory))
            {

                // Because the buffer size is one and we are failing we should block on the dispatch
                Task routing = router.RouteAsync(message1);

                var endpoint2 = new TestEndpoint("endpoint1");
                var newRoute = new Route("id", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
                Task setting = router.SetRoute(newRoute);

                Task timeout = Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                await Task.WhenAny(routing, setting, timeout);
                await router.CloseAsync(CancellationToken.None);
                var expected = new List<IMessage> { message1 };
                Assert.Equal(expected, endpoint2.Processed);
            }
        }

        [Fact, Unit]
        public async Task TestOffset()
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(s => s.GetCheckpointDataAsync("router.1", It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(22));
            store.Setup(s => s.GetCheckpointDataAsync("router.1.endpoint1", It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(23));
            store.Setup(s => s.GetCheckpointDataAsync("router.1.endpoint2", It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(22));

            var endpoint1 = new TestEndpoint("endpoint1");
            var endpoint2 = new TestEndpoint("endpoint2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };

            var routes = new HashSet<Route>
            {
                new Route("route1", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 }),
                new Route("route2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 })
            };

            Router router1 = await Router.CreateAsync("router.1", "hub", new RouterConfig(allEndpoints, routes, Fallback), SyncExecutorFactory, store.Object);
            Assert.Equal(Option.Some(22L), router1.Offset);

            store.Verify(s => s.GetCheckpointDataAsync("router.1", It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.GetCheckpointDataAsync("router.1.endpoint1", It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.GetCheckpointDataAsync("router.1.endpoint2", It.IsAny<CancellationToken>()), Times.Once);

            await router1.RouteAsync(MessageWithOffset(25L));
            Assert.Equal(Option.Some(25L), router1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync("router.1", It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync("router.1.endpoint1", It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync("router.1.endpoint2", It.Is<CheckpointData>(c => c.Offset == 25), It.IsAny<CancellationToken>()), Times.Once);

            await router1.RemoveRoute("route1");
            await router1.RouteAsync(MessageWithOffset(26L));
            Assert.Equal(Option.Some(26L), router1.Offset);
            store.Verify(s => s.SetCheckpointDataAsync("router.1", It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.SetCheckpointDataAsync("router.1.endpoint1", It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Never);
            store.Verify(s => s.SetCheckpointDataAsync("router.1.endpoint2", It.Is<CheckpointData>(c => c.Offset == 26), It.IsAny<CancellationToken>()), Times.Once);

            await router1.CloseAsync(CancellationToken.None);

            Router router2 = await Router.CreateAsync("router2", "hub", new RouterConfig(allEndpoints, new HashSet<Route>(), Fallback), SyncExecutorFactory);
            Assert.Equal(Option.None<long>(), router2.Offset);
            await router2.CloseAsync(CancellationToken.None);
        }

        [Fact, Unit]
        public async Task TestFallback()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] {1, 2, 3}, new Dictionary<string, string> { {"key1", "value1"}, {"key2", "value2"} });
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route = new Route("id", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var fallback = new Route("$fallback", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
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
    }
}
