// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Test.Endpoints;
    using Xunit;

    [Unit]
    public class RouteResultTest
    {
        [Theory]
        [MemberData(nameof(GetTestData))]
        public void EqualityTest(RouteResult r1, RouteResult r2, bool areEqual)
        {
            Assert.Equal(areEqual, r1.Equals(r2));
            Assert.Equal(areEqual, r1.Equals((object)r2));
            Assert.Equal(areEqual, r1.GetHashCode() == r2.GetHashCode());
        }

        public static IEnumerable<object[]> GetTestData()
        {
            var endpoint1 = new TestEndpoint("endpoint1");
            var endpoint2 = new TestEndpoint("endpoint2");

            var route1 = new RouteResult(endpoint1, 0, 30);
            var route2 = new RouteResult(endpoint1, 0, 30);
            var route3 = new RouteResult(endpoint2, 0, 30);
            var route4 = new RouteResult(endpoint1, 1, 30);
            var route5 = new RouteResult(endpoint1, 0, 60);
            var route6 = new RouteResult(endpoint2, 2, 90);

            yield return new object[] { route1, route2, true };
            yield return new object[] { route1, route3, false };    // Endpoint is different
            yield return new object[] { route1, route4, false };    // Priority is different
            yield return new object[] { route1, route5, false };    // TTL is different
            yield return new object[] { route1, route5, false };    // TTL is different
            yield return new object[] { route1, route6, false };    // Everything is different
        }
    }
}
