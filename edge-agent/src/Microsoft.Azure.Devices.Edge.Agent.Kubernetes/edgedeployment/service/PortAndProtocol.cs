// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PortAndProtocol
    {
        public int Port { get; }

        public string Protocol { get; }

        PortAndProtocol(int port, string protocol)
        {
            this.Port = port;
            this.Protocol = protocol;
        }

        public static Option<PortAndProtocol> Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Option.None<PortAndProtocol>();
            }

            string[] portProtocol = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (portProtocol.Length != 2)
            {
                return Option.None<PortAndProtocol>();
            }

            if (!int.TryParse(portProtocol[0], out int port) || !SupportedProtocols.TryGetValue(portProtocol[1], out string protocol))
            {
                return Option.None<PortAndProtocol>();
            }

            return Option.Some(new PortAndProtocol(port, protocol));
        }

        static readonly Dictionary<string, string> SupportedProtocols = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            ["TCP"] = "TCP",
            ["UDP"] = "UDP",
            ["SCTP"] = "SCTP",
        };
    }
}
