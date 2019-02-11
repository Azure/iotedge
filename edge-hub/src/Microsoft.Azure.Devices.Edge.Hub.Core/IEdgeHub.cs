// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

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

        Task<DirectMethodResponse> InvokeMethodAsync(string id, DirectMethodRequest methodRequest);

        Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage);

        Task<IMessage> GetTwinAsync(string id);

        Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection);

        Task SendC2DMessageAsync(string id, IMessage message);

        Task AddSubscription(string id, DeviceSubscription deviceSubscription);

        Task RemoveSubscription(string id, DeviceSubscription deviceSubscription);

        Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions);
    }
}
