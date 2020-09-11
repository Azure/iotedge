// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PortAndProtocol
    {
        static Option<PortAndProtocol> empty = Option.None<PortAndProtocol>();
        static string defaultProtocol = "TCP";

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
                return empty;
            }

            string[] portProtocol = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (portProtocol.Length > 2)
            {
                return empty;
            }

            if (!int.TryParse(portProtocol[0], out int port))
            {
                return empty;
            }

            // Docker defaults to TCP if not specified.
            string protocol = defaultProtocol;
            if (portProtocol.Length > 1)
            {
                if (!SupportedProtocols.TryGetValue(portProtocol[1], out protocol))
                {
                    return empty;
                }
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
