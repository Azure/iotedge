// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class RouterFactoryTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public async Task TestConstructor()
        {
            var factory = new RouterFactory(new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions));
            Router router = await factory.CreateAsync("id1", "hub1");

            Assert.Equal("id1", router.Id);
            Assert.Equal(0, router.Routes.Count);
        }
    }
}
