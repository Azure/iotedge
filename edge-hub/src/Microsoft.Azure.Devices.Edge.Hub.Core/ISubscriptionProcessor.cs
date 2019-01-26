// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface ISubscriptionProcessor
    {
        Task ProcessSubscription(string id, DeviceSubscription deviceSubscription, bool addSubscription);

        Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions);
    }
}
