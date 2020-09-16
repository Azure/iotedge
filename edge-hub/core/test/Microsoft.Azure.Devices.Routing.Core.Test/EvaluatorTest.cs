// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class EvaluatorTest : RoutingUnitTestBase
    {
        static readonly IMessageSource InvalidMessageSource = CustomMessageSource.Create("/invalid/message/path");
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage InvalidMessage = new Message(InvalidMessageSource, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value3" }, { "key2", "value2" } });
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value4" }, { "key2", "value2" } });

        [Fact]
        [Unit]
        public void SmokeTest()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));
            ISet<RouteResult> results = evaluator.Evaluate(Message1);
            Assert.Equal(1, results.Count);
            Assert.Equal(endpoint2, results.First().Endpoint);
        }

        [Fact]
        [Unit]
        public void TestSetRoute()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var result1 = new RouteResult(endpoint1, 0, 0);
            var result2 = new RouteResult(endpoint2, 0, 0);
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<RouteResult> results = evaluator.Evaluate(Message1);
            Assert.Equal(1, results.Count);
            Assert.Equal(endpoint2, results.First().Endpoint);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Add route 3
            evaluator.SetRoute(route3);
            results = evaluator.Evaluate(Message1);
            Assert.Equal(2, results.Count);
            Assert.Contains(result1, results);
            Assert.Contains(result2, results);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route2, route1 }));

            // Replace route2
            var endpoint3 = new TestEndpoint("id3");
            var result3 = new RouteResult(endpoint3, 0, 0);
            var route4 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, endpoint3, 0, 0);
            evaluator.SetRoute(route4);
            results = evaluator.Evaluate(Message1);
            Assert.Equal(2, results.Count);
            Assert.Contains(result1, results);
            Assert.Contains(result3, results);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route4, route1 }));
        }

        [Fact]
        [Unit]
        public void TestRemoveRoute()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var result1 = new RouteResult(endpoint1, 0, 0);
            var result2 = new RouteResult(endpoint2, 0, 0);
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route2, route1, route3 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<RouteResult> results = evaluator.Evaluate(Message1);
            Assert.Equal(2, results.Count);
            Assert.Contains(result2, results);
            Assert.Contains(result1, results);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Remove route2
            evaluator.RemoveRoute("id2");
            results = evaluator.Evaluate(Message1);
            Assert.Equal(1, results.Count);
            Assert.Contains(result1, results);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route1 }));

            // Remove route3
            evaluator.RemoveRoute("id3");
            results = evaluator.Evaluate(Message1);
            Assert.Equal(0, results.Count);
            Assert.DoesNotContain(result1, results);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route1 }));

            // Remove route3 again
            evaluator.RemoveRoute("id3");
            results = evaluator.Evaluate(Message1);
            Assert.Equal(0, results.Count);
            Assert.DoesNotContain(result1, results);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route1 }));
        }

        [Fact]
        [Unit]
        public void TestReplaceRoutes()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 0);
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<RouteResult> results = evaluator.Evaluate(Message1);
            Assert.Equal(1, results.Count);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Add route 3 and remove route 1
            var newRoutes = new HashSet<Route> { route2, route3 };
            evaluator.ReplaceRoutes(newRoutes);
            results = evaluator.Evaluate(Message1);
            Assert.Equal(2, results.Count);
            Assert.True(evaluator.Routes.SetEquals(newRoutes));
        }

        [Fact]
        [Unit]
        public void TestMessageSource()
        {
            var endpoint1 = new NullEndpoint("id1");
            var endpoint3 = new NullEndpoint("id3");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint3 };
            var route1 = new Route("id1", "true", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var route3 = new Route("id3", "true", "hub", InvalidMessageSource, endpoint3, 0, 0);
            var routes = new HashSet<Route> { route1, route3 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            NullEndpoint[] expected = new[] { endpoint1, endpoint3 };
            IMessage[] messages = new[] { Message1, InvalidMessage };
            foreach (Tuple<NullEndpoint, IMessage> pair in expected.Zip(messages, Tuple.Create))
            {
                ISet<RouteResult> results = evaluator.Evaluate(pair.Item2);

                Assert.Equal(1, results.Count);
                Assert.Contains(new RouteResult(pair.Item1, 0, 0), results);
            }
        }

        [Fact]
        [Unit]
        public void TestFallback()
        {
            var endpoint1 = new NullEndpoint("id1");
            var endpoint2 = new NullEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var fallback = new Route("$fallback", "true", "hub", InvalidMessageSource, endpoint2, 0, 0);
            var routes = new HashSet<Route> { route1 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes, Option.Some(fallback)));

            ISet<RouteResult> result1 = evaluator.Evaluate(Message1);
            Assert.Equal(1, result1.Count);
            Assert.Equal(endpoint1, result1.First().Endpoint);

            ISet<RouteResult> result2 = evaluator.Evaluate(Message4);
            Assert.Equal(1, result2.Count);
            Assert.Equal(endpoint2, result2.First().Endpoint);

            // non-telemetry messages should not send to the fallback
            ISet<RouteResult> result3 = evaluator.Evaluate(InvalidMessage);
            Assert.Equal(0, result3.Count);
        }

        [Fact]
        [Unit]
        public void TestNoFallback()
        {
            var endpoint1 = new NullEndpoint("id1");
            var allEndpoints = new HashSet<Endpoint> { endpoint1 };
            var route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 0);
            var routes = new HashSet<Route> { route1 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<RouteResult> result1 = evaluator.Evaluate(Message1);
            Assert.Equal(1, result1.Count);
            Assert.Equal(endpoint1, result1.First().Endpoint);

            ISet<RouteResult> result2 = evaluator.Evaluate(Message4);
            Assert.Equal(0, result2.Count);
        }

        [Fact]
        [Unit]
        public void TestPriorities()
        {
            var endpoint1 = new NullEndpoint("endpoint1");
            var endpoint2 = new NullEndpoint("endpoint2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint1, 1, 3600);
            var route2 = new Route("id2", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint1, 0, 7200);
            var route3 = new Route("id3", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint1, 2, 60);
            var route4 = new Route("id4", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, endpoint2, 0, 120);
            var routes = new HashSet<Route> { route1, route2, route3, route4 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            // Verify multiple routes to the same endpoint with different priorities,
            // the route with highest priority should win
            ISet<RouteResult> results = evaluator.Evaluate(Message1);
            Assert.Equal(2, results.Count);
            Assert.Contains(new RouteResult(endpoint1, 0, 7200), results);
            Assert.Contains(new RouteResult(endpoint2, 0, 120), results);
        }
    }
}
