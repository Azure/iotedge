// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface ICloud2DeviceMessageHandler
    {
        Task SendC2DMessageAsync(IMessage message, IIdentity identity, bool isDirectClient);
        IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions { get; }
    }
}
