// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRouteStore
    {
        Task<RouterConfig> GetRouterConfigAsync(string iotHubName, CancellationToken token);
    }
}