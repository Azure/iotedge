// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sources
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Sources;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class NullSourceFactoryTest : RoutingUnitTestBase
    {
        static readonly IEndpointExecutorFactory ExecutorFactory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(new HashSet<Endpoint>(), new HashSet<Route>(), Option.None<Route>()), ExecutorFactory);
            var factory = new NullSourceFactory();
            Source source = await factory.CreateAsync("hub", router, CancellationToken.None);
            Assert.IsType<NullSource>(source);
        }
    }
}
