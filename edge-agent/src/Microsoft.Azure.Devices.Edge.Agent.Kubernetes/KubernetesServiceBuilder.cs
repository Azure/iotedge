// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;

    public class KubernetesServiceBuilder
    {
        readonly string defaultMapServiceType;

        public KubernetesServiceBuilder(string defaultMapServiceType)
        {
            this.defaultMapServiceType = defaultMapServiceType;
        }

        public Option<V1Service> GetServiceFromModule(Dictionary<string, string> labels, IModule<AgentDocker.CombinedDockerConfig> module, IModuleIdentity moduleIdentity)
        {
            var portList = new List<V1ServicePort>();
            Option<Dictionary<string, string>> serviceAnnotations = Option.None<Dictionary<string, string>>();
            bool onlyExposedPorts = true;

            if (module.Config.CreateOptions?.Labels != null)
            {
                // Add annotations from Docker labels. This provides the customer a way to assign annotations to services if they want
                // to tie backend services to load balancers via an Ingress Controller.
                var annotations = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> label in module.Config.CreateOptions?.Labels)
                {
                    annotations.Add(KubeUtils.SanitizeAnnotationKey(label.Key), label.Value);
                }

                serviceAnnotations = Option.Some(annotations);
            }

            // Handle ExposedPorts entries
            if (module.Config?.CreateOptions?.ExposedPorts != null)
            {
                // Entries in the Exposed Port list just tell Docker that this container wants to listen on that port.
                // We interpret this as a "ClusterIP" service type listening on that exposed port, backed by this module.
                // Users of this Module's exposed port should be able to find the service by connecting to "<module name>:<port>"
                PortExtensions.GetExposedPorts(module.Config.CreateOptions.ExposedPorts)
                    .ForEach(
                        exposedList =>
                            exposedList.ForEach((item) => portList.Add(new V1ServicePort(item.Port, name: $"ExposedPort-{item.Port}-{item.Protocol.ToLower()}", protocol: item.Protocol))));
            }

            // Handle HostConfig PortBindings entries
            if (module.Config?.CreateOptions?.HostConfig?.PortBindings != null)
            {
                foreach (KeyValuePair<string, IList<PortBinding>> portBinding in module.Config?.CreateOptions?.HostConfig?.PortBindings)
                {
                    string[] portProtocol = portBinding.Key.Split('/');
                    if (portProtocol.Length == 2)
                    {
                        if (int.TryParse(portProtocol[0], out int port) && ProtocolExtensions.TryValidateProtocol(portProtocol[1], out string protocol))
                        {
                            // Entries in Docker portMap wants to expose a port on the host (hostPort) and map it to the container's port (port)
                            // We interpret that as the pod wants the cluster to expose a port on a public IP (hostPort), and target it to the container's port (port)
                            foreach (PortBinding hostBinding in portBinding.Value)
                            {
                                if (int.TryParse(hostBinding.HostPort, out int hostPort))
                                {
                                    // If a port entry contains the same "port", then remove it and replace with a new ServicePort that contains a target.
                                    var duplicate = portList.SingleOrDefault(a => a.Port == hostPort);
                                    if (duplicate != default(V1ServicePort))
                                    {
                                        portList.Remove(duplicate);
                                    }

                                    portList.Add(new V1ServicePort(hostPort, name: $"HostPort-{port}-{protocol.ToLower()}", protocol: protocol, targetPort: port));
                                    onlyExposedPorts = false;
                                }
                                else
                                {
                                    Events.PortBindingValue(module, portBinding.Key);
                                }
                            }
                        }
                    }
                }
            }

            if (portList.Count > 0)
            {
                // Selector: by module name and device name, also how we will label this puppy.
                var objectMeta = new V1ObjectMeta(annotations: serviceAnnotations.GetOrElse(() => null), labels: labels, name: KubeUtils.SanitizeDNSValue(moduleIdentity.ModuleId));
                // How we manage this service is dependent on the port mappings user asks for.
                // If the user tells us to only use ClusterIP ports, we will always set the type to ClusterIP.
                // If all we had were exposed ports, we will assume ClusterIP. Otherwise, we use the given value as the default service type
                //
                // If the user wants to expose the ClusterIPs port externally, they should manually create a service to expose it.
                // This gives the user more control as to how they want this to work.
                string serviceType;
                if (onlyExposedPorts)
                {
                    serviceType = "ClusterIP";
                }
                else
                {
                    serviceType = this.defaultMapServiceType;
                }

                return Option.Some(new V1Service(metadata: objectMeta, spec: new V1ServiceSpec(type: serviceType, ports: portList, selector: labels)));
            }
            else
            {
                return Option.None<V1Service>();
            }
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesServiceBuilder;
            private static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesServiceBuilder>();

            enum EventIds
            {
                PortBindingValue = IdStart,
            }

            public static void PortBindingValue(IModule module, string portEntry)
            {
                Log.LogWarning((int)EventIds.PortBindingValue, $"Module {module.Name} has an invalid port binding value '{portEntry}'.");
            }
        }
    }
}
