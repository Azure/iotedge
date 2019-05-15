// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class NullSubscriptionProcessor : ISubscriptionProcessor
    {
        public NullSubscriptionProcessor()
        {
            Events.DisableCloudSubscriptions();
        }

        public Task AddSubscription(string id, DeviceSubscription deviceSubscription) => Task.CompletedTask;

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription) => Task.CompletedTask;

        public Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions) => Task.CompletedTask;

        static class Events
        {
            const int IdStart = HubCoreEventIds.SubscriptionProcessor;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SubscriptionProcessor>();

            enum EventIds
            {
                DisableCloudSubscriptions = IdStart
            }

            public static void DisableCloudSubscriptions()
            {
                Log.LogWarning((int)EventIds.DisableCloudSubscriptions, "Cloud subscriptions disabled - no subscriptions will be sent to the cloud. Local subscriptions will work.");
            }
        }
    }
}
