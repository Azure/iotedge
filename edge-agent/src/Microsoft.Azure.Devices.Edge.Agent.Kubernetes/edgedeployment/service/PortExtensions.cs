// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public static class PortExtensions
    {
        public static List<V1ContainerPort> GetContainerPorts(this IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> ports)
            => ports.Select(port => ExtractContainerPort(port.Key)).FilterMap().ToList();

        static Option<V1ContainerPort> ExtractContainerPort(string exposedPort)
            => PortAndProtocol.Parse(exposedPort)
                .Map(portAndProtocol => new V1ContainerPort(portAndProtocol.Port, protocol: portAndProtocol.Protocol));

        public static List<V1ServicePort> GetExposedPorts(this IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> ports)
            => ports.Select(port => ExtractExposedPort(port.Key)).FilterMap().ToList();

        static Option<V1ServicePort> ExtractExposedPort(string exposedPort) =>
            PortAndProtocol.Parse(exposedPort)
                .Map(portAndProtocol => new V1ServicePort(portAndProtocol.Port, name: $"ExposedPort-{portAndProtocol.Port}-{portAndProtocol.Protocol}".ToLowerInvariant(), protocol: portAndProtocol.Protocol));

        public static List<V1ServicePort> GetHostPorts(this IDictionary<string, IList<PortBinding>> ports)
            => ports.SelectMany(port => ExtractHostPorts(port.Key, port.Value)).ToList();

        static IEnumerable<V1ServicePort> ExtractHostPorts(string name, IEnumerable<PortBinding> bindings)
            =>
                PortAndProtocol.Parse(name)
                    .Map(
                        portAndProtocol =>
                            bindings.Select(
                                    hostBinding =>
                                    {
                                        if (int.TryParse(hostBinding.HostPort, out int hostPort))
                                        {
                                            return Option.Some(new V1ServicePort(hostPort, name: $"HostPort-{portAndProtocol.Port}-{portAndProtocol.Protocol}".ToLowerInvariant(), protocol: portAndProtocol.Protocol, targetPort: portAndProtocol.Port));
                                        }

                                        Events.PortBindingValue(name);
                                        return Option.None<V1ServicePort>();
                                    })
                                .FilterMap()
                                .ToList())
                    .GetOrElse(() => new List<V1ServicePort>());

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesModelValidation;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesServiceMapper>();

            enum EventIds
            {
                InvalidExposedPortValue = IdStart,
                PortBindingValue
            }

            public static void InvalidExposedPortValue(string portEntry)
            {
                Log.LogWarning((int)EventIds.InvalidExposedPortValue, $"Received an invalid exposed port value '{portEntry}'.");
            }

            public static void PortBindingValue(string portEntry)
            {
                Log.LogWarning((int)EventIds.PortBindingValue, $"Received invalid port binding value '{portEntry}'.");
            }
        }
    }
}
