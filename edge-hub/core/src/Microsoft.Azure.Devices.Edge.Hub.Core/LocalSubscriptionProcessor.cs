// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LocalSubscriptionProcessor : SubscriptionProcessorBase
    {
        public LocalSubscriptionProcessor(IConnectionManager connectionManager)
            : base(connectionManager)
        {
            Events.DisableCloudSubscriptions();
        }

        protected override void HandleSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptions)
        {
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.SubscriptionProcessor;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SubscriptionProcessor>();

            enum EventIds
            {
                LocalSubscriptions = IdStart
            }

            public static void DisableCloudSubscriptions()
            {
                Log.LogWarning((int)EventIds.LocalSubscriptions, "Cloud subscriptions disabled - no subscriptions will be sent to the cloud. Only local subscriptions will work.");
            }
        }
    }
}
