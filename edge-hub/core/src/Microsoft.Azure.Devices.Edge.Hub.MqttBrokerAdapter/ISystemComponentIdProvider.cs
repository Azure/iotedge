// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public interface ISystemComponentIdProvider
    {
        string EdgeHubBridgeId { get; }
        bool IsSystemComponent(string id);
    }
}
