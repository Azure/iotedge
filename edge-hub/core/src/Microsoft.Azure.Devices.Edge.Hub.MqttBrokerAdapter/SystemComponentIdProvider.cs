// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class SystemComponentIdProvider : ISystemComponentIdProvider
    {
        static string[] knownSystemComponents = new[] { "$bridge", "$broker" };

        public SystemComponentIdProvider(IClientCredentials edgeHubCredentials)
        {
            this.EdgeHubBridgeId = edgeHubCredentials.Identity.Id + "/$bridge";
        }

        public string EdgeHubBridgeId { get; }

        public bool IsSystemComponent(string id)
        {
            var segments = id.Split('/');
            return segments.Length == 3 && knownSystemComponents.Contains(segments[2]);
        }
    }
}
