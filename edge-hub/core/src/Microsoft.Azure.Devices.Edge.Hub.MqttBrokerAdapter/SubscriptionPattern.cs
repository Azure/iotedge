// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SubscriptionPattern
    {
        public SubscriptionPattern(string pattern, DeviceSubscription subscription)
        {
            this.Pattern = Preconditions.CheckNotNull(pattern);
            this.Subscription = subscription;
        }

        public string Pattern { get; }
        public DeviceSubscription Subscription { get; }
    }
}
