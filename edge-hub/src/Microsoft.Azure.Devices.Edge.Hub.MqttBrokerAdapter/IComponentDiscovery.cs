// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;

    public interface IComponentDiscovery
    {
        IReadOnlyCollection<IMqttSubscriber> Subscribers { get; }
        IReadOnlyCollection<IMqttMessageProducer> Producers { get; }
        IReadOnlyCollection<IMqttMessageConsumer> Consumers { get; }
    }
}
