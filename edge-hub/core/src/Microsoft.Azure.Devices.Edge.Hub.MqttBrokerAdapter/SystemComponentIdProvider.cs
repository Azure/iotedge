// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class SystemComponentIdProvider : ISystemComponentIdProvider
    {
        public SystemComponentIdProvider(IClientCredentials edgeHubCredentials)
        {
            this.EdgeHubBridgeId = edgeHubCredentials.Identity.Id + "/$bridge";
        }

        public string EdgeHubBridgeId { get; }
    }
}
