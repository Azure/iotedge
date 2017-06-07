// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    class TwinSubscriptionRegistration : ISubscriptionRegistration
    {
        public Task ProcessSubscriptionAsync(ICloudProxy cp)
        {
            return cp.SetupDesiredPropertyUpdatesAsync();
        }
    }
}