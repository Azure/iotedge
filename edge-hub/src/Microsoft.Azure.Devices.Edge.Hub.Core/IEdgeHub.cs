// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The <c>IEdgeHub</c> is responsible for processing messages sent to the
    /// edge hub by devices and modules. The <see cref="Microsoft.Azure.Devices.Edge.Hub.Core.Routing.RoutingEdgeHub"/>
    /// for instance handles this by having the router process the message by
    /// executing the routing rules it is configured with.
    /// </summary>
    public interface IEdgeHub : IDisposable
    {
        Task ProcessDeviceMessage(IIdentity identity, IMessage message);

        Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> message);

        Task<DirectMethodResponse> InvokeMethodAsync(IIdentity identity, DirectMethodRequest methodRequest);

        Task SendMethodResponseAsync(DirectMethodResponse response);

        Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage);
    }
}