// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Application settings, derived from "appsetting_k8s.json" and environment
    /// variables, loaded from autofac. These setting are used to allow edgeAgent
    /// to properly construct proxy container settings and for agent to build itself.
    /// </summary>
    public class KubernetesApplicationSettings
    {
        // Location of "appsetting_k8s.json" in container
        public string AgentConfigPath { get; set; }
        // Name of configmap containing "appsetting_k8s.json"
        public string AgentConfigMapName { get; set; }
        // Name of volume to use when mounting configmap, used in PodSpec
        public string AgentConfigVolume { get; set; }
        // Proxy Image
        public string ProxyImage { get; set; }
        // Name of volume to use when mounting proxy's configmap
        public string ProxyConfigVolume { get; set; }
        // Name of configmap containing "config.yaml"
        public string ProxyConfigMapName { get; set; }
        // Location of "config.yaml" in proxy container
        public string ProxyConfigPath { get; set; }
        // Location of trustbundle in proxy container
        public string ProxyTrustBundlePath { get; set; }
        // Name of volume to use when mounting trustbundle
        public string ProxyTrustBundleVolume { get; set; }
        // Map of configmap containing trustbundle
        public string ProxyTrustBundleConfigMapName { get; set; }
        // Resource requirements for proxy.
        public ResourceSettings ProxyResourceRequests { get; set; }
        // Resource requirements for agent.
        public ResourceSettings AgentResourceRequests { get; set; }

        public Option<V1ResourceRequirements> GetProxyResourceRequirements() =>
                    Option.Maybe(this.ProxyResourceRequests).Map(rr => rr.ToResourceRequirements());
        public Option<V1ResourceRequirements> GetAgentResourceRequirements() =>
                    Option.Maybe(this.AgentResourceRequests).Map(rr => rr.ToResourceRequirements());
    }
}
