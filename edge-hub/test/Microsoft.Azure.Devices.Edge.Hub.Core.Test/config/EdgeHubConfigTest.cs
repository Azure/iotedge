// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeHubConfigTest
    {
        [Theory]
        [MemberData(nameof(GetEdgeHubConfigData))]
        public void EqualityTest(EdgeHubConfig e1, EdgeHubConfig e2, bool areEqual)
        {
            Assert.Equal(areEqual, e1.Equals(e2));
            Assert.Equal(areEqual, e1 == e2);
            Assert.Equal(!areEqual, e1 != e2);
            Assert.Equal(areEqual, e1.Equals((object)e2));
            Assert.Equal(areEqual, e1.GetHashCode() == e2.GetHashCode());
        }

        public static IEnumerable<object[]> GetEdgeHubConfigData()
        {
            var r1 = new Route("id", string.Empty, "iotHub", Mock.Of<IMessageSource>(), new HashSet<Endpoint>());
            var r2 = new Route("id", string.Empty, "iotHub", Mock.Of<IMessageSource>(), new HashSet<Endpoint>());

            var routeConfig1 = new RouteConfig("r1", "FROM /* INTO $upstream", r1);
            var routeConfig2 = new RouteConfig("r2", "FROM /messages/* INTO $upstream", r2);

            var routes1 = new Dictionary<string, RouteConfig>
            {
                [routeConfig1.Name] = routeConfig1,
                [routeConfig2.Name] = routeConfig2
            };

            var routes2 = new Dictionary<string, RouteConfig>
            {
                [routeConfig1.Name] = routeConfig1
            };

            var routes3 = new Dictionary<string, RouteConfig>
            {
                [routeConfig2.Name] = routeConfig2
            };

            var storeAndForwardConfig1 = new StoreAndForwardConfiguration(-1);
            var storeAndForwardConfig2 = new StoreAndForwardConfiguration(7200);
            var storeAndForwardConfig3 = new StoreAndForwardConfiguration(3600);
            var storeAndForwardConfig4 = new StoreAndForwardConfiguration(3600, Option.Some(10L));
            var storeAndForwardConfig5 = new StoreAndForwardConfiguration(3600, Option.Some(20L));
            var storeAndForwardConfig6 = new StoreAndForwardConfiguration(3600, Option.None<long>());

            string version = "1.0";

            var edgeHubConfig1 = new EdgeHubConfig(version, routes1, storeAndForwardConfig1);
            var edgeHubConfig2 = new EdgeHubConfig(version, routes2, storeAndForwardConfig1);
            var edgeHubConfig3 = new EdgeHubConfig(version, routes3, storeAndForwardConfig1);
            var edgeHubConfig4 = new EdgeHubConfig(version, routes1, storeAndForwardConfig1);
            var edgeHubConfig5 = new EdgeHubConfig(version, routes1, storeAndForwardConfig2);
            var edgeHubConfig6 = new EdgeHubConfig(version, routes1, storeAndForwardConfig3);
            var edgeHubConfig7 = new EdgeHubConfig(version, routes2, storeAndForwardConfig2);
            var edgeHubConfig8 = new EdgeHubConfig(version, routes2, storeAndForwardConfig3);
            var edgeHubConfig9 = new EdgeHubConfig(version, routes3, storeAndForwardConfig3);
            var edgeHubConfig10 = new EdgeHubConfig(version, routes3, storeAndForwardConfig3);
            var edgeHubConfig11 = new EdgeHubConfig(version, routes3, storeAndForwardConfig4);
            var edgeHubConfig12 = new EdgeHubConfig(version, routes3, storeAndForwardConfig5);
            var edgeHubConfig13 = new EdgeHubConfig(version, routes3, storeAndForwardConfig6);

            yield return new object[] { edgeHubConfig1, edgeHubConfig2, false };
            yield return new object[] { edgeHubConfig2, edgeHubConfig3, false };
            yield return new object[] { edgeHubConfig3, edgeHubConfig4, false };
            yield return new object[] { edgeHubConfig4, edgeHubConfig5, false };
            yield return new object[] { edgeHubConfig5, edgeHubConfig6, false };
            yield return new object[] { edgeHubConfig6, edgeHubConfig7, false };
            yield return new object[] { edgeHubConfig7, edgeHubConfig8, false };
            yield return new object[] { edgeHubConfig8, edgeHubConfig9, false };
            yield return new object[] { edgeHubConfig9, edgeHubConfig10, true };
            yield return new object[] { edgeHubConfig10, edgeHubConfig11, false };
            yield return new object[] { edgeHubConfig11, edgeHubConfig12, false };
            yield return new object[] { edgeHubConfig10, edgeHubConfig13, true };
            yield return new object[] { edgeHubConfig12, edgeHubConfig13, false };
        }
    }
}
