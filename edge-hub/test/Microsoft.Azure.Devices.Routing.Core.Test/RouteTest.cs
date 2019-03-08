// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class RouteTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

        static readonly Endpoint Endpoint1 = new TestEndpoint("id1");
        static readonly Endpoint Endpoint2 = new TestEndpoint("id2");
        static readonly Endpoint Endpoint3 = new TestEndpoint("id1");

        static readonly Route Route1 = new Route("id1", "rule1", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });
        static readonly Route Route2 = new Route("id1", "rule1", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint2 });
        static readonly Route Route3 = new Route("id1", "rule1", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint3 });
        static readonly Route Route4 = new Route("id2", "rule2", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });
        static readonly Route Route5 = new Route("id3", "rule3", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
        static readonly Route Route6 = new Route("id3", "rule3", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
        static readonly Route Route7 = new Route("id2", "rule1", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { Endpoint1 });

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new Route(null, "condition", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>()));
            Assert.Throws<ArgumentNullException>(() => new Route("id", null, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>()));
            Assert.Throws<ArgumentNullException>(() => new Route("id", "condition", null, TelemetryMessageSource.Instance, new HashSet<Endpoint>()));
            Assert.Throws<ArgumentNullException>(() => new Route("id", "condition", "hub", TelemetryMessageSource.Instance, null));
        }

        [Fact]
        [Unit]
        public void SmokeTest()
        {
            var route = new Route("id", "true", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { new TestEndpoint("id1") });
            Func<IMessage, Bool> evaluate = RouteCompiler.Instance.Compile(route);
            Assert.True(evaluate(Message1));
        }

        [Fact]
        [Unit]
        public void TestShow()
        {
            var route = new Route("id1", "select *", "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint> { new TestEndpoint("id1"), new TestEndpoint("id2") });
            string expected1 = "Route(\"id1\", TelemetryMessageSource, \"select *\" => (TestEndpoint(id1), TestEndpoint(id2))";
            string expected2 = "Route(\"id1\", TelemetryMessageSource, \"select *\" => (TestEndpoint(id2), TestEndpoint(id1))";
            Assert.True(expected1.Equals(route.ToString()) || expected2.Equals(route.ToString()));
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Assert.Equal(Route1, Route1);
            Assert.Equal(Route1, Route3);
            Assert.NotEqual(Route1, Route2);
            Assert.NotEqual(Route1, Route4);
            Assert.NotEqual(Route1, Route5);
            Assert.NotEqual(Route1, Route7);
            Assert.Equal(Route5, Route6);

            Assert.False(Route1.Equals(null));

            Assert.True(Route1.Equals(Route1));
            Assert.False(Route1.Equals(Route2));

            Assert.False(Route1.Equals(null));
            Assert.False(Route1.Equals((object)null));
            Assert.True(Route1.Equals((object)Route1));
            Assert.False(Route1.Equals((object)Route2));
            Assert.False(Route1.Equals(new object()));
        }

        [Fact]
        [Unit]
        public void TestHashCode()
        {
            ISet<Route> routes = new HashSet<Route>
            {
                Route1,
                Route2,
                Route3,
                Route4,
                Route5,
                Route6
            };
            Assert.Equal(4, routes.Count);
            Assert.Contains(Route1, routes);
            Assert.Contains(Route2, routes);
            Assert.Contains(Route4, routes);
            Assert.Contains(Route5, routes);
        }

        [Theory]
        [Unit]
        [InlineData("appKey", 1)]
        [InlineData("true", 1)]
        [InlineData("$body.Value = 3", 3)]
        [InlineData("true or true", 3)]
        [InlineData("none = 'true' or true", 5)]
        [InlineData("{$body.message.Weather.Location.State} <> 'CA'", 5)]
        [InlineData("is_defined(3 % 0)", 6)]
        [InlineData("power($body.message.Weather.Temperature, length($body.message.Weather.Location.State)) = square($body.message.Weather.Temperature)", 14)]
        [InlineData("is_defined(x) and power(as_number(x),as_number(y))", 17)]
        public void TestRouteComplexity(string condition, int expected)
        {
            var testRoute = new Route(
                "id1",
                condition,
                "hub",
                TelemetryMessageSource.Instance,
                new HashSet<Endpoint>
                {
                    Endpoint1
                });

            int complexity = RouteCompiler.Instance.GetComplexity(testRoute);
            Assert.Equal(expected, complexity);
        }
    }
}
