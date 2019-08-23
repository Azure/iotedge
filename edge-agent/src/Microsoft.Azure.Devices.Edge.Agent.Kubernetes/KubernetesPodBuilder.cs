// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using DockerModels = global::Docker.DotNet.Models;

    public class KubernetesPodBuilder
    {
        const string SocketDir = "/var/run/iotedge";
        const string ConfigVolumeName = "config-volume";
        const string TrustBundleVolumeName = "trust-bundle-volume";
        const string SocketVolumeName = "workload";

        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;

        public KubernetesPodBuilder(string proxyImage, string proxyConfigPath, string proxyConfigVolumeName, string proxyTrustBundlePath, string proxyTrustBundleVolumeName)
        {
            this.proxyImage = proxyImage;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
        }

        public V1PodTemplateSpec GetPodFromModule(Dictionary<string, string> labels, IModule<AgentDocker.CombinedDockerConfig> module, IModuleIdentity moduleIdentity, List<V1EnvVar> envVars)
        {
            // pod labels
            var podLabels = new Dictionary<string, string>(labels);

            // pod annotations
            var podAnnotations = new Dictionary<string, string>();
            podAnnotations.Add(Constants.K8sEdgeOriginalModuleId, moduleIdentity.ModuleId);

            // Convert docker labels to annotations because docker labels don't have the same restrictions as
            // Kuberenetes labels.
            if (module.Config.CreateOptions?.Labels != null)
            {
                foreach ((string key, string label) in module.Config.CreateOptions?.Labels)
                {
                    podAnnotations.Add(KubeUtils.SanitizeAnnotationKey(key), label);
                }
            }

            // Per container settings:
            // exposed ports
            Option<List<V1ContainerPort>> exposedPortsOption = Option.Maybe(module.Config?.CreateOptions?.ExposedPorts)
                .FlatMap(ports => PortExtensions.GetExposedPorts(ports))
                .Map(ports => ports.Select(tuple => new V1ContainerPort(tuple.Port, protocol: tuple.Protocol)).ToList());

            // privileged container
            Option<V1SecurityContext> securityContext = Option.Maybe(module.Config?.CreateOptions?.HostConfig?.Privileged)
                .Map(config => new V1SecurityContext(privileged: true));

            // Bind mounts
            (List<V1Volume> volumeList, List<V1VolumeMount> proxyMounts, List<V1VolumeMount> volumeMountList) = this.GetVolumesFromModule(module);

            // Image
            string moduleImage = module.Config.Image;

            var name = KubeUtils.SanitizeDNSValue(moduleIdentity.ModuleId);

            var containerList = new List<V1Container>
            {
                new V1Container(
                    name,
                    env: envVars,
                    image: moduleImage,
                    volumeMounts: volumeMountList,
                    securityContext: securityContext.OrDefault(),
                    ports: exposedPortsOption.OrDefault()),

                // TODO: Add Proxy container here - configmap for proxy configuration.
                new V1Container(
                    "proxy",
                    env: envVars, // TODO: check these for validity for proxy.
                    image: this.proxyImage,
                    volumeMounts: proxyMounts)
            };

            Option<List<V1LocalObjectReference>> imageSecret = module.Config.AuthConfig.Map(
                auth =>
                {
                    var secret = new ImagePullSecret(auth);
                    var authList = new List<V1LocalObjectReference>
                    {
                        new V1LocalObjectReference(secret.Name)
                    };
                    return authList;
                });

            var objectMeta = new V1ObjectMeta(name: name, labels: podLabels, annotations: podAnnotations);

            var modulePodSpec = new V1PodSpec(
                containerList,
                volumes: volumeList,
                imagePullSecrets: imageSecret.OrDefault(),
                serviceAccountName: name
            );

            return new V1PodTemplateSpec(objectMeta, modulePodSpec);
        }

        (List<V1Volume>, List<V1VolumeMount>, List<V1VolumeMount>) GetVolumesFromModule(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
        {
            var volumeList = new List<V1Volume>
            {
                new V1Volume(SocketVolumeName, emptyDir: new V1EmptyDirVolumeSource()),
                new V1Volume(ConfigVolumeName, configMap: new V1ConfigMapVolumeSource(name: this.proxyConfigVolumeName)),
                new V1Volume(TrustBundleVolumeName, configMap: new V1ConfigMapVolumeSource(name: this.proxyTrustBundleVolumeName))
            };

            var proxyMountList = new List<V1VolumeMount>
            {
                new V1VolumeMount(SocketDir, SocketVolumeName),
                new V1VolumeMount(this.proxyConfigPath, ConfigVolumeName),
                new V1VolumeMount(this.proxyTrustBundlePath, TrustBundleVolumeName)
            };

            var volumeMountList = new List<V1VolumeMount>(proxyMountList);

            if (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Binds != null)
            {
                foreach (string bind in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Binds)
                {
                    string[] bindSubstrings = bind.Split(':');
                    if (bindSubstrings.Length >= 2)
                    {
                        string name = KubeUtils.SanitizeDNSValue(bindSubstrings[0]);
                        string type = "DirectoryOrCreate";
                        string hostPath = bindSubstrings[0];
                        volumeList.Add(new V1Volume(name, hostPath: new V1HostPathVolumeSource(hostPath, type)));

                        string mountPath = bindSubstrings[1];
                        bool readOnly = bindSubstrings.Length > 2 && bindSubstrings[2].Contains("ro");
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                }
            }

            if (moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts != null)
            {
                foreach (DockerModels.Mount mount in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts)
                {
                    if (mount.Type.Equals("bind", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = KubeUtils.SanitizeDNSValue(mount.Source);
                        string type = "DirectoryOrCreate";
                        string hostPath = mount.Source;
                        volumeList.Add(new V1Volume(name, hostPath: new V1HostPathVolumeSource(hostPath, type)));

                        string mountPath = mount.Target;
                        bool readOnly = mount.ReadOnly;
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                    else if (mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = KubeUtils.SanitizeDNSValue(mount.Source);
                        string mountPath = mount.Target;
                        volumeList.Add(new V1Volume(name, emptyDir: new V1EmptyDirVolumeSource()));

                        bool readOnly = mount.ReadOnly;
                        volumeMountList.Add(new V1VolumeMount(mountPath, name, readOnlyProperty: readOnly));
                    }
                }
            }

            return (volumeList, proxyMountList, volumeMountList);
        }
    }
}
