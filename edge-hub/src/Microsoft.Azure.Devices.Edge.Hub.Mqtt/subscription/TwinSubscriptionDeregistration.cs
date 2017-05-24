// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    class TwinSubscriptionDeregistration : ISubscriptionRegistration
    {
        public Task ProcessSubscriptionAsync(ICloudProxy cp)
        {
            throw new NotImplementedException();
        }
    }
}