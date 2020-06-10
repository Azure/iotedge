// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;

    public class GenericSubscriptions : ISubscriber
    {
        const string SubscriptionChangeDevice = "$edgehub/+/subscriptions";
        const string SubscriptionChangeModule = "$edgehub/+/+/subscriptions";

        static readonly string[] subscriptions = new[] { SubscriptionChangeDevice, SubscriptionChangeModule };

        public IReadOnlyCollection<string> Subscriptions => subscriptions;
    }
}
