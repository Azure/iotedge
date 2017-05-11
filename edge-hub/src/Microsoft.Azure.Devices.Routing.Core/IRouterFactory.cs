// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public interface IRouterFactory
    {
        Task<Router> CreateAsync(string id, string iotHubName);

        Task<Router> CreateAsync(string id, string iotHubName, ISet<Endpoint> endpoints, ISet<Route> routes, Option<Route> fallback);
    }
}