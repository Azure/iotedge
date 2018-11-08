// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.RouteFactory
{
    using System;

    using RouteFactory = Microsoft.Azure.Devices.Routing.Core.RouteFactory;

    public class TestRouteFactory : RouteFactory
    {
        public TestRouteFactory(IEndpointFactory endpointFactory)
            : base(endpointFactory)
        {
        }

        public override string IotHubName => "TestIoTHub";

        public override string GetNextRouteId() => Guid.NewGuid().ToString();
    }
}
