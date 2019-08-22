// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using DockerModels = global::Docker.DotNet.Models;

    public static class V1PodSpecEx
    {
        public static bool PodSpecEquals(V1PodSpec self, V1PodSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(self, other))
            {
                return true;
            }

            List<V1Container> otherList = other.Containers.ToList();

            foreach (V1Container selfContainer in self.Containers)
            {
                if (otherList.Exists(c => string.Equals(c.Name, selfContainer.Name)))
                {
                    V1Container otherContainer = otherList.Find(c => string.Equals(c.Name, selfContainer.Name));
                    if (!string.Equals(selfContainer.Image, otherContainer.Image))
                    {
                        // Container has a new image name.
                        return false;
                    }
                }
                else
                {
                    // container names don't match
                    return false;
                }
            }

            return true;
        }
    }

    public static class V1PodTemplateSpecEx
    {
        public static bool ImageEquals(V1PodTemplateSpec self, V1PodTemplateSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(self, other))
            {
                return true;
            }

            return V1PodSpecEx.PodSpecEquals(self.Spec, other.Spec);
        }
    }

    public static class V1DeploymentSpecEx
    {
        public static bool ImageEquals(V1DeploymentSpec self, V1DeploymentSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(self, other))
            {
                return true;
            }

            return V1PodTemplateSpecEx.ImageEquals(self.Template, other.Template);
        }
    }

    public static class V1DeploymentEx
    {
        public static bool ImageEquals(V1Deployment self, V1Deployment other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(self, other))
            {
                return true;
            }

            return string.Equals(self.Kind, other.Kind) &&
                V1DeploymentSpecEx.ImageEquals(self.Spec, other.Spec);
        }
    }

    public static class ProtocolExtensions
    {
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
    }

    public static class PortExtensions
    {
        public static Option<List<(int Port, string Protocol)>> GetExposedPorts(IDictionary<string, DockerModels.EmptyStruct> exposedPorts)
        {
            var serviceList = new List<(int, string)>();
            foreach (KeyValuePair<string, DockerModels.EmptyStruct> exposedPort in exposedPorts)
            {
                string[] portProtocol = exposedPort.Key.Split('/');
                if (portProtocol.Length == 2)
                {
                    if (int.TryParse(portProtocol[0], out int port) && ProtocolExtensions.TryValidateProtocol(portProtocol[1], out string protocol))
                    {
                        serviceList.Add((port, protocol));
                    }
                    else
                    {
                        Events.ExposedPortValue(exposedPort.Key);
                    }
                }
            }

            return (serviceList.Count > 0) ? Option.Some(serviceList) : Option.None<List<(int, string)>>();
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesModuleBuilder;
            private static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesPodBuilder>();

            enum EventIds
            {
                ExposedPortValue = IdStart,
            }

            public static void ExposedPortValue(string portEntry)
            {
                Log.LogWarning((int)EventIds.ExposedPortValue, $"Received an invalid exposed port value '{portEntry}'.");
            }
        }
    }
}
