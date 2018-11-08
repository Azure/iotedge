// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRoutingService : IDisposable
    {
        Task CloseAsync(CancellationToken token);

        Task<IEnumerable<EndpointHealthData>> GetEndpointHealthAsync(string hubName);

        Task RouteAsync(string hubName, IMessage message);

        Task RouteAsync(string hubName, IEnumerable<IMessage> messages);

        Task StartAsync();
    }
}
