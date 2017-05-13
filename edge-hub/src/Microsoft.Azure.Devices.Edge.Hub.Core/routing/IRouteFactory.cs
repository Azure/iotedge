// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Routing.Core;

    public interface IRouteFactory
    {
        Route Create(string route);

        IEnumerable<Route> Create(IEnumerable<string> routes);
    }
}