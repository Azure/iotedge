// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageConsumer
    {
        Task<bool> HandleAsync(MqttPublishInfo publishInfo);
        IReadOnlyCollection<string> Subscriptions { get; }
    }
}
