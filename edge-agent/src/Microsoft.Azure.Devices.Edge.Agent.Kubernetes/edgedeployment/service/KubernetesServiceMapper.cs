// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    // TODO add unit tests
    public class KubernetesServiceMapper : IKubernetesServiceMapper
    {
        readonly PortMapServiceType defaultMapServiceType;

        public KubernetesServiceMapper(PortMapServiceType defaultMapServiceType)
        {
            this.defaultMapServiceType = defaultMapServiceType;
        }

        public Option<V1Service> CreateService(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            Option<V1Service> service = this.PrepareService(identity, module, labels);
            service.ForEach(
                s => s.Metadata.Annotations = new Dictionary<string, string>
                {
                    [KubernetesConstants.CreationString] = JsonConvert.SerializeObject(s)
                });

            return service;
        }

        public void UpdateService(V1Service to, V1Service from)
        {
            to.Metadata.ResourceVersion = from.Metadata.ResourceVersion;
            to.Spec.ClusterIP = from.Spec.ClusterIP;
        }

        Option<V1Service> PrepareService(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            var portList = new List<V1ServicePort>();
            bool onlyExposedPorts = true;

            // Add annotations from Docker labels. This provides the customer a way to assign annotations to services if they want
            // to tie backend services to load balancers via an Ingress Controller.
            var annotations = Option.Maybe(module.Config.CreateOptions?.Labels)
                .Map(dockerLabels => dockerLabels.ToDictionary(label => KubeUtils.SanitizeAnnotationKey(label.Key), label => label.Value))
                .GetOrElse(() => new Dictionary<string, string>());

            // Handle ExposedPorts entries
            if (module.Config?.CreateOptions?.ExposedPorts != null)
            {
                // Entries in the Exposed Port list just tell Docker that this container wants to listen on that port.
                // We interpret this as a "ClusterIP" service type listening on that exposed port, backed by this module.
                // Users of this Module's exposed port should be able to find the service by connecting to "<module name>:<port>"
                module.Config.CreateOptions.ExposedPorts.GetExposedPorts()
                    .ForEach(ports => ports.ForEach(item => portList.Add(new V1ServicePort(item.Port, $"ExposedPort-{item.Port}-{item.Protocol.ToLower()}", protocol: item.Protocol))));
            }

            // Handle HostConfig PortBindings entries
            if (module.Config?.CreateOptions?.HostConfig?.PortBindings != null)
            {
                foreach (KeyValuePair<string, IList<PortBinding>> portBinding in module.Config?.CreateOptions?.HostConfig?.PortBindings)
                {
                    string[] portProtocol = portBinding.Key.Split('/');
                    if (portProtocol.Length == 2)
                    {
                        if (int.TryParse(portProtocol[0], out int port) && PortExtensions.TryValidateProtocol(portProtocol[1], out string protocol))
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

                                    var portName = $"hostport-{port}-{protocol.ToLower()}";
                                    portList.Add(new V1ServicePort(hostPort, portName, null, protocol, port));
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
                var name = identity.DeploymentName();
                var objectMeta = new V1ObjectMeta(annotations: annotations, labels: labels, name: name);

                // How we manage this service is dependent on the port mappings user asks for.
                // If the user tells us to only use ClusterIP ports, we will always set the type to ClusterIP.
                // If all we had were exposed ports, we will assume ClusterIP. Otherwise, we use the given value as the default service type
                //
                // If the user wants to expose the ClusterIPs port externally, they should manually create a service to expose it.
                // This gives the user more control as to how they want this to work.
                var serviceType = onlyExposedPorts
                    ? PortMapServiceType.ClusterIP
                    : this.defaultMapServiceType;

                return Option.Some(new V1Service(metadata: objectMeta, spec: new V1ServiceSpec(type: serviceType.ToString(), ports: portList, selector: labels)));
            }

            return Option.None<V1Service>();
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesServiceBuilder;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesServiceMapper>();

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
