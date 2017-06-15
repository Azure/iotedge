// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    /// <summary>
    /// The <c>IEdgeHub</c> is responsible for processing messages sent to the
    /// edge hub by devices and modules. The <see cref="Microsoft.Azure.Devices.Edge.Hub.Core.Routing.RoutingEdgeHub"/>
    /// for instance handles this by having the router process the message by
    /// executing the routing rules it is configured with.
    /// </summary>
    public interface IEdgeHub
    {
        Task ProcessDeviceMessage(IIdentity identity, IMessage message);

        Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> message);

        Task<DirectMethodResponse> InvokeMethodAsync(IIdentity identity, DirectMethodRequest methodRequest);

        Task SendMethodResponseAsync(DirectMethodResponse response);
    }
}