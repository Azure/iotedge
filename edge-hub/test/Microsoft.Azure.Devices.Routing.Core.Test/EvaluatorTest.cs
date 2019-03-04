// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Util;
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
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));
            ISet<Endpoint> endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(1, endpoints.Count);
            Assert.Contains(endpoint2, endpoints);
        }

        [Fact]
        [Unit]
        public void TestSetRoute()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<Endpoint> endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(1, endpoints.Count);
            Assert.Contains(endpoint2, endpoints);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Add route 3
            evaluator.SetRoute(route3);
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(2, endpoints.Count);
            Assert.Contains(endpoint1, endpoints);
            Assert.Contains(endpoint2, endpoints);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route2, route1 }));

            // Replace route2
            var endpoint3 = new TestEndpoint("id3");
            var route4 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint3 });
            evaluator.SetRoute(route4);
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(2, endpoints.Count);
            Assert.Contains(endpoint1, endpoints);
            Assert.Contains(endpoint3, endpoints);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route4, route1 }));
        }

        [Fact]
        [Unit]
        public void TestRemoveRoute()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var routes = new HashSet<Route> { route2, route1, route3 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<Endpoint> endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(2, endpoints.Count);
            Assert.Contains(endpoint2, endpoints);
            Assert.Contains(endpoint1, endpoints);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Remove route2
            evaluator.RemoveRoute("id2");
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(1, endpoints.Count);
            Assert.Contains(endpoint1, endpoints);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route3, route1 }));

            // Remove route3
            evaluator.RemoveRoute("id3");
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(0, endpoints.Count);
            Assert.DoesNotContain(endpoint1, endpoints);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route1 }));

            // Remove route3 again
            evaluator.RemoveRoute("id3");
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(0, endpoints.Count);
            Assert.DoesNotContain(endpoint1, endpoints);
            Assert.True(evaluator.Routes.SetEquals(new HashSet<Route> { route1 }));
        }

        [Fact]
        [Unit]
        public void TestReplaceRoutes()
        {
            var endpoint1 = new TestEndpoint("id1");
            var endpoint2 = new TestEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "false", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route2 = new Route("id2", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint2 });
            var route3 = new Route("id3", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var routes = new HashSet<Route> { route2, route1 };

            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<Endpoint> endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(1, endpoints.Count);
            Assert.True(routes.SetEquals(evaluator.Routes));

            // Add route 3 and remove route 1
            var newRoutes = new HashSet<Route> { route2, route3 };
            evaluator.ReplaceRoutes(newRoutes);
            endpoints = evaluator.Evaluate(Message1);
            Assert.Equal(2, endpoints.Count);
            Assert.True(evaluator.Routes.SetEquals(newRoutes));
        }

        [Fact]
        [Unit]
        public void TestMessageSource()
        {
            var endpoint1 = new NullEndpoint("id1");
            var endpoint3 = new NullEndpoint("id3");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint3 };
            var route1 = new Route("id1", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var route3 = new Route("id3", "true", "hub", InvalidMessageSource, new HashSet<Endpoint> { endpoint3 });
            var routes = new HashSet<Route> { route1, route3 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            NullEndpoint[] expected = new[] { endpoint1, endpoint3 };
            IMessage[] messages = new[] { Message1, InvalidMessage };
            foreach (Tuple<NullEndpoint, IMessage> pair in expected.Zip(messages, Tuple.Create))
            {
                ISet<Endpoint> result = evaluator.Evaluate(pair.Item2);

                Assert.Equal(1, result.Count);
                Assert.Contains(pair.Item1, result);
            }
        }

        [Fact]
        [Unit]
        public void TestFallback()
        {
            var endpoint1 = new NullEndpoint("id1");
            var endpoint2 = new NullEndpoint("id2");
            var allEndpoints = new HashSet<Endpoint> { endpoint1, endpoint2 };
            var route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var fallback = new Route("$fallback", "true", "hub", InvalidMessageSource, new HashSet<Endpoint> { endpoint2 });
            var routes = new HashSet<Route> { route1 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes, Option.Some(fallback)));

            ISet<Endpoint> result1 = evaluator.Evaluate(Message1);
            Assert.Equal(1, result1.Count);
            Assert.Contains(endpoint1, result1);

            ISet<Endpoint> result2 = evaluator.Evaluate(Message4);
            Assert.Equal(1, result2.Count);
            Assert.Contains(endpoint2, result2);

            // non-telemetry messages should not send to the fallback
            ISet<Endpoint> result3 = evaluator.Evaluate(InvalidMessage);
            Assert.Equal(0, result3.Count);
        }

        [Fact]
        [Unit]
        public void TestNoFallback()
        {
            var endpoint1 = new NullEndpoint("id1");
            var allEndpoints = new HashSet<Endpoint> { endpoint1 };
            var route1 = new Route("id1", "key1 = 'value1'", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { endpoint1 });
            var routes = new HashSet<Route> { route1 };
            var evaluator = new Evaluator(new RouterConfig(allEndpoints, routes));

            ISet<Endpoint> result1 = evaluator.Evaluate(Message1);
            Assert.Equal(1, result1.Count);
            Assert.Contains(endpoint1, result1);

            ISet<Endpoint> result2 = evaluator.Evaluate(Message4);
            Assert.Equal(0, result2.Count);
        }
    }
}
