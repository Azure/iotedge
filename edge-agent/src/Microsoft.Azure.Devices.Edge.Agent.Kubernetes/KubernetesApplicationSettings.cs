// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesApplicationSettings
    {
        public string AgentConfigPath { get; set; }
        public string AgentConfigMapName { get; set; }
        public string AgentConfigVolume { get; set; }
        public string ProxyImage { get; set; }
        public string ProxyConfigVolume { get; set; }
        public string ProxyConfigMapName { get; set; }
        public string ProxyConfigPath { get; set; }
        public string ProxyTrustBundlePath { get; set; }
        public string ProxyTrustBundleVolume { get; set; }
        public string ProxyTrustBundleConfigMapName { get; set; }
        public ResourceSettings ProxyResourceRequests { get; set; }
        public ResourceSettings AgentResourceRequests { get; set; }

        public Option<V1ResourceRequirements> GetProxyResourceRequirements() =>
                    Option.Maybe(this.ProxyResourceRequests).Map(rr => rr.ToResourceRequirements());
        public Option<V1ResourceRequirements> GetAgentResourceRequirements() =>
                    Option.Maybe(this.AgentResourceRequests).Map(rr => rr.ToResourceRequirements());
    }
}
