// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using DockerModels = global::Docker.DotNet.Models;

    public class KubernetesModelBuilder<TConfig>
    {
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string defaultMapServiceType;

        private KubernetesServiceBuilder<TConfig> serviceBuilder;
        private KubernetesPodBuilder<TConfig> podBuilder;

        Dictionary<string, string> currentModuleLabels;
        KubernetesModule<TConfig> currentModule;
        IModuleIdentity currentModuleIdentity;
        List<V1EnvVar> currentModuleEnvVars;

        public KubernetesModelBuilder(string proxyImage, string proxyConfigPath, string proxyConfigVolumeName, string proxyTrustBundlePath, string proxyTrustBundleVolumeName, string defaultMapServiceType)
        {
            this.proxyImage = proxyImage;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
            this.defaultMapServiceType = defaultMapServiceType;
        }

        public void LoadModule(Dictionary<string, string> labels, KubernetesModule<TConfig> module, IModuleIdentity moduleIdentity, List<V1EnvVar> envVars)
        {
            this.currentModuleLabels = labels;
            this.currentModule = module;
            this.currentModuleIdentity = moduleIdentity;
            this.currentModuleEnvVars = envVars;

            this.serviceBuilder = new KubernetesServiceBuilder<TConfig>(this.defaultMapServiceType);
            this.podBuilder = new KubernetesPodBuilder<TConfig>(this.proxyImage, this.proxyConfigPath, this.proxyConfigVolumeName, this.proxyTrustBundlePath, this.proxyTrustBundleVolumeName);
        }

        public Option<V1Service> GetService()
        {
            return this.serviceBuilder.GetServiceFromModule(this.currentModuleLabels, this.currentModule, this.currentModuleIdentity);
        }

        public V1PodTemplateSpec GetPod()
        {
            return this.podBuilder.GetPodFromModule(this.currentModuleLabels, this.currentModule, this.currentModuleIdentity, this.currentModuleEnvVars);
        }

        private Option<List<(int Port, string Protocol)>> GetExposedPorts(IDictionary<string, DockerModels.EmptyStruct> exposedPorts)
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
            const int IdStart = KubernetesEventIds.KubernetesModelBuilder;
            private static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesServiceBuilder<TConfig>>();

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
