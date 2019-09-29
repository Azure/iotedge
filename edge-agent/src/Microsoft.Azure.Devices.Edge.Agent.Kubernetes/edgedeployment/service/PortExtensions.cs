// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public static class PortExtensions
    {
        public static Option<List<(int Port, string Protocol)>> GetExposedPorts(this IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> exposedPorts)
        {
            var serviceList = new List<(int, string)>();
            foreach (KeyValuePair<string, global::Docker.DotNet.Models.EmptyStruct> exposedPort in exposedPorts)
            {
                string[] portProtocol = exposedPort.Key.Split('/');
                if (portProtocol.Length == 2)
                {
                    if (int.TryParse(portProtocol[0], out int port) && TryValidateProtocol(portProtocol[1], out string protocol))
                    {
                        serviceList.Add((port, protocol));
                    }
                    else
                    {
                        Events.InvalidExposedPortValue(exposedPort.Key);
                    }
                }
            }

            return serviceList.Count > 0 ? Option.Some(serviceList) : Option.None<List<(int, string)>>();
        }

        public static bool TryValidateProtocol(string dockerProtocol, out string k8SProtocol)
        {
            bool result = true;
            switch (dockerProtocol.ToUpper())
            {
                case "TCP":
                    k8SProtocol = "TCP";
                    break;
                case "UDP":
                    k8SProtocol = "UDP";
                    break;
                case "SCTP":
                    k8SProtocol = "SCTP";
                    break;
                default:
                    k8SProtocol = "TCP";
                    result = false;
                    break;
            }

            return result;
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesModelValidation;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesServiceMapper>();

            enum EventIds
            {
                InvalidExposedPortValue = IdStart,
            }

            public static void InvalidExposedPortValue(string portEntry)
            {
                Log.LogWarning((int)EventIds.InvalidExposedPortValue, $"Received an invalid exposed port value '{portEntry}'.");
            }
        }
    }
}
