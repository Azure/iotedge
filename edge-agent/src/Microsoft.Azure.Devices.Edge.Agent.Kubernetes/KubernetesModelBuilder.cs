// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesModelBuilder
    {
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyConfigMapName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string proxyTrustBundleConfigMapName;
        readonly PortMapServiceType defaultMapServiceType;

        KubernetesServiceBuilder serviceBuilder;
        KubernetesPodBuilder podBuilder;

        Dictionary<string, string> currentModuleLabels;
        IModule<CombinedDockerConfig> currentModule;
        IModuleIdentity currentModuleIdentity;
        List<V1EnvVar> currentModuleEnvVars;

        public KubernetesModelBuilder(
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyConfigMapName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string proxyTrustBundleConfigMapName,
            PortMapServiceType defaultMapServiceType)
        {
            this.proxyImage = proxyImage;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyConfigMapName = proxyConfigMapName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
            this.proxyTrustBundleConfigMapName = proxyTrustBundleConfigMapName;
            this.defaultMapServiceType = defaultMapServiceType;
        }

        public void LoadModule(Dictionary<string, string> labels, IModule<CombinedDockerConfig> module, IModuleIdentity moduleIdentity, List<V1EnvVar> envVars)
        {
            this.currentModuleLabels = labels;
            this.currentModuleIdentity = moduleIdentity;
            this.currentModuleEnvVars = envVars;
            this.currentModule = module;

            this.serviceBuilder = new KubernetesServiceBuilder(this.defaultMapServiceType);
            this.podBuilder = new KubernetesPodBuilder(this.proxyImage, this.proxyConfigPath, this.proxyConfigVolumeName, this.proxyConfigMapName, this.proxyTrustBundlePath, this.proxyTrustBundleVolumeName, this.proxyTrustBundleConfigMapName);
        }

        public Option<V1Service> GetService()
        {
            return this.serviceBuilder.GetServiceFromModule(this.currentModuleLabels, this.currentModule, this.currentModuleIdentity);
        }

        public V1PodTemplateSpec GetPod()
        {
            return this.podBuilder.GetPodFromModule(this.currentModuleLabels, this.currentModule, this.currentModuleIdentity, this.currentModuleEnvVars);
        }
    }
}
