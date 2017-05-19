// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class RouteStoreTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public async Task SmokeTest()
        {
            var store1 = new RouteStore();
            RouterConfig config = await store1.GetRouterConfigAsync("hub", CancellationToken.None);
            Assert.Equal(0, config.Routes.Count);
            Endpoint endpoint1 = new NullEndpoint("endpoint1");
            Endpoint endpoint2 = new NullEndpoint("endpoint2");
            IEnumerable<Endpoint> allEndpoints = new List<Endpoint> {  endpoint1, endpoint2 };
            var route1 = new Route("id1", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route2 = new Route("id2", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
            IEnumerable<Route> allRoutes = new List<Route> { route1, route2 };
            var store2 = new RouteStore(new Dictionary<string, RouterConfig>
            {
                { "hub", new RouterConfig(allEndpoints, allRoutes) }
            });
            RouterConfig config2 = await store2.GetRouterConfigAsync("hub", CancellationToken.None);
            Assert.True(config2.Routes.SetEquals(new HashSet<Route> { route1, route2 }));

            RouterConfig config3 = await store2.GetRouterConfigAsync("hub2", CancellationToken.None);
            Assert.Equal(0, config3.Routes.Count);
        }
    }
}