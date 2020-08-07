// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces
{
    public interface IMessageHandler
    {
        Task<bool> ProcessMessageAsync(string topic, byte[] payload);
    }
}
