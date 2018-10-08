// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;

    using Microsoft.Azure.Devices.Routing.Core;

    public class EdgeRouteFactory : RouteFactory
    {
        public EdgeRouteFactory(IEndpointFactory endpointFactory)
            : base(endpointFactory)
        {
        }

        // This is not being used in the routing code. So hardcode it to fixed value.
        public override string IotHubName => "IotHub";

        public override string GetNextRouteId() => Guid.NewGuid().ToString();
    }
}
