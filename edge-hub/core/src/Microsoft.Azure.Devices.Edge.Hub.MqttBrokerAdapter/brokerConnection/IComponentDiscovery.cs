// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;

    public interface IComponentDiscovery
    {
        IReadOnlyCollection<IMessageProducer> Producers { get; }
        IReadOnlyCollection<IMessageConsumer> Consumers { get; }
    }
}
