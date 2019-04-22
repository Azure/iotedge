// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Services;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Moq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class FilteringRoutingServiceTest
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value2" }, { "key2", "value2" } });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 2, 1 }, new Dictionary<string, string> { { "key1", "value3" }, { "key2", "value2" } });
        static readonly Endpoint Endpoint1 = new NullEndpoint("id1");
        static readonly Route Route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });
        static readonly Route Route2 = new Route("id2", "key1 = 'value2'", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });
        static readonly Route Route3 = new Route("id3", "key1 = 'value3'", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });
        static readonly IEnumerable<Endpoint> AllEndpoints = new List<Endpoint> { Endpoint1 };
        static readonly IEnumerable<Route> AllRoutes = new List<Route> { Route1, Route2, Route3 };
        static readonly IRouteStore RouteStore;

        static FilteringRoutingServiceTest()
        {
            RouteStore = new RouteStore(
                new Dictionary<string, RouterConfig>
                {
                    { "hub1", new RouterConfig(AllEndpoints, AllRoutes) }
                });
        }

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var underlying = new Mock<IRoutingService>();
            var client = new FilteringRoutingService(underlying.Object, RouteStore, NullNotifierFactory.Instance);

            // Check proxies methods
            await client.StartAsync();
            underlying.Verify(s => s.StartAsync(), Times.Once);

            // Check lets messages through
            await client.RouteAsync("hub1", Message1);
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message1 }), Times.Once);

            await client.RouteAsync("hub1", new[] { Message2, Message3 });
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message2, Message3 }), Times.Once);

            // Check filters out messages
            await client.RouteAsync("hub2", new[] { Message2, Message3 });
            underlying.Verify(s => s.RouteAsync("hub2", It.IsAny<IEnumerable<IMessage>>()), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task TestRules()
        {
            var store = new RouteStore(
                new Dictionary<string, RouterConfig>
                {
                    { "hub1", new RouterConfig(AllEndpoints, new List<Route> { Route1 }) }
                });
            var underlying = new Mock<IRoutingService>();
            var client = new FilteringRoutingService(underlying.Object, store, NullNotifierFactory.Instance);

            await client.RouteAsync("hub1", new[] { Message1, Message2 });
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message1 }), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task TestClose()
        {
            var underlying = new Mock<IRoutingService>();
            using (var client = new FilteringRoutingService(underlying.Object, RouteStore, NullNotifierFactory.Instance))
            {
                // Create at least one evaluator
                await client.RouteAsync("hub1", Message1);

                await client.CloseAsync(CancellationToken.None);
                // can close twice
                await client.CloseAsync(CancellationToken.None);

                await Assert.ThrowsAsync<InvalidOperationException>(() => client.RouteAsync("hub1", Message1));
            }
        }

        [Fact]
        [Unit]
        public async Task TestChangingHub()
        {
            var notifier = new TestNotifier();
            var notifierFactory = new TestNotifierFactory(notifier);
            var underlying = new Mock<IRoutingService>();
            var store = new Mock<IRouteStore>();

            store.Setup(s => s.GetRouterConfigAsync("hub1", It.IsAny<CancellationToken>())).ReturnsAsync(new RouterConfig(AllEndpoints, new[] { Route1 }, Option.None<Route>()));
            store.Setup(s => s.GetRouterConfigAsync("hub2", It.IsAny<CancellationToken>())).ReturnsAsync(new RouterConfig(AllEndpoints, new[] { Route2 }, Option.None<Route>()));

            var client = new FilteringRoutingService(underlying.Object, store.Object, notifierFactory);
            await client.RouteAsync("hub1", new[] { Message1, Message2, Message3 });
            await client.RouteAsync("hub2", new[] { Message1, Message2, Message3 });
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message1 }), Times.Once);
            underlying.Verify(s => s.RouteAsync("hub2", new[] { Message2 }), Times.Once);

            // change hub1
            underlying.Invocations.Clear();
            store.Setup(s => s.GetRouterConfigAsync("hub1", It.IsAny<CancellationToken>())).ReturnsAsync(new RouterConfig(AllEndpoints, new[] { Route2 }, Option.None<Route>()));
            await notifier.Change("hub1");

            await client.RouteAsync("hub1", new[] { Message1, Message2, Message3 });
            await client.RouteAsync("hub2", new[] { Message1, Message2, Message3 });
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message2 }), Times.Once);
            underlying.Verify(s => s.RouteAsync("hub2", new[] { Message2 }), Times.Once);

            // change hub2
            underlying.Invocations.Clear();
            store.Setup(s => s.GetRouterConfigAsync("hub2", It.IsAny<CancellationToken>())).ReturnsAsync(new RouterConfig(AllEndpoints, new[] { Route3 }, Option.None<Route>()));
            await notifier.Change("hub2");

            await client.RouteAsync("hub1", new[] { Message1, Message2, Message3 });
            await client.RouteAsync("hub2", new[] { Message1, Message2, Message3 });
            underlying.Verify(s => s.RouteAsync("hub1", new[] { Message2 }), Times.Once);
            underlying.Verify(s => s.RouteAsync("hub2", new[] { Message3 }), Times.Once);
        }
    }
}
