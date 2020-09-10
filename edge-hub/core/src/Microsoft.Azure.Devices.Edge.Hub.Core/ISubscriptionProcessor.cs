// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface ISubscriptionProcessor
    {
        Task AddSubscription(string id, DeviceSubscription deviceSubscription);

        Task RemoveSubscription(string id, DeviceSubscription deviceSubscription);

        Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions);
    }
}
