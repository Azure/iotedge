// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;

    public interface ISubscriptionWatcher
    {
        IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions { get; }
    }
}
