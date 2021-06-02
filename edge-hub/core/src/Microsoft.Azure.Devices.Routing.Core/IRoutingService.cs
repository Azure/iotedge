// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRoutingService : IDisposable
    {
        Task RouteAsync(string hubName, IMessage message);

        Task RouteAsync(string hubName, IEnumerable<IMessage> messages);

        Task<IEnumerable<EndpointHealthData>> GetEndpointHealthAsync(string hubName);

        Task StartAsync();

        Task CloseAsync(CancellationToken token);
    }
}