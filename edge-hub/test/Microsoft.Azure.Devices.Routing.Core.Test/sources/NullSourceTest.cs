// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
    public class NullSourceTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var executorFactory = new AsyncEndpointExecutorFactory(TestConstants.DefaultConfig, TestConstants.DefaultOptions);
            Router router = await Router.CreateAsync("router1", "hub", new RouterConfig(new HashSet<Endpoint>(), new HashSet<Route>(), Option.None<Route>()), executorFactory);
            var source = new NullSource(router);
            Task result = source.CloseAsync(CancellationToken.None);
            Assert.True(result.IsCompleted);
        }
    }
}
