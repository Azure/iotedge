// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public class SubscriptionPattern
    {
        public SubscriptionPattern(string pattern, DeviceSubscription subscription)
        {
            this.Pattern = pattern;
            this.Subscrition = subscription;
        }

        public string Pattern { get; }
        public DeviceSubscription Subscrition { get; }
    }
}
