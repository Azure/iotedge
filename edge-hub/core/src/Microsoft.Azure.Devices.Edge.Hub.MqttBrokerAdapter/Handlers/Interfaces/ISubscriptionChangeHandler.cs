// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Threading.Tasks;

    public interface ISubscriptionChangeHandler
    {
        Task<bool> HandleSubscriptionChangeAsync(MqttPublishInfo publishInfo);
    }
}
