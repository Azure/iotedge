// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;

    public static class AvailableTcpPorts
    {
        public static int Next(int source)
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            HashSet<int> activePorts = new HashSet<int>(properties.GetActiveTcpListeners().Select(endpoint => endpoint.Port));

            return Enumerable.Range(source, ushort.MaxValue).First(port => activePorts.Contains(port));
        }
    }
}
