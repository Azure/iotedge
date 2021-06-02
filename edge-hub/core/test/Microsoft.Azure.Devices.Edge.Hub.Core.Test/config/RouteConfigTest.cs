// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [Unit]
    public class RouteConfigTest
    {
        [Theory]
        [MemberData(nameof(GetEqualityTest))]
        public void EqualityTest(RouteConfig c1, RouteConfig c2, bool isEqual)
        {
            Assert.Equal(isEqual, c1.Equals(c2));
        }

        public static IEnumerable<object[]> GetEqualityTest()
        {
            var r1 = new Route("id", string.Empty, "iotHub", Mock.Of<IMessageSource>(), new Mock<Endpoint>("endpoint1").Object, 0, 3600);
            var r2 = new Route("id", string.Empty, "iotHub", Mock.Of<IMessageSource>(), new Mock<Endpoint>("endpoint2").Object, 0, 3600);
            var routeConfig1 = new RouteConfig("r1", "FROM /* INTO $upstream", r1);
            var routeConfig2 = new RouteConfig("r1", "FROM /* INTO $upstream", r2);
            var routeConfig3 = new RouteConfig("r2", "FROM /* INTO $upstream", r2);
            var routeConfig4 = new RouteConfig("r2", "FROM /messages/* INTO $upstream", r2);
            var routeConfig5 = new RouteConfig("r2", "FROM /messages/* INTO $upstream", r2);

            yield return new object[] { routeConfig1, routeConfig2, false };
            yield return new object[] { routeConfig2, routeConfig3, false };
            yield return new object[] { routeConfig3, routeConfig4, false };
            yield return new object[] { routeConfig4, routeConfig5, true };
        }
    }
}
