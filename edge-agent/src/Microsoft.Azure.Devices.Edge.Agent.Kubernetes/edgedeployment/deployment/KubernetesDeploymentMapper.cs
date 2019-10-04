// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesDeploymentMapper : IKubernetesDeploymentMapper
    {
        const string EdgeHubHostname = "edgehub";

        readonly string deviceNamespace;
        readonly string edgeHostname;
        readonly string proxyImage;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyConfigMapName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string proxyTrustBundleConfigMapName;
        readonly string workloadApiVersion;
        readonly Uri workloadUri;
        readonly Uri managementUri;

        public KubernetesDeploymentMapper(
            string deviceNamespace,
            string edgeHostname,
            string proxyImage,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyConfigMapName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string proxyTrustBundleConfigMapName,
            string workloadApiVersion,
            Uri workloadUri,
            Uri managementUri)
        {
            this.deviceNamespace = deviceNamespace;
            this.edgeHostname = edgeHostname;
            this.proxyImage = proxyImage;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyConfigMapName = proxyConfigMapName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
            this.proxyTrustBundleConfigMapName = proxyTrustBundleConfigMapName;
            this.workloadApiVersion = workloadApiVersion;
            this.workloadUri = workloadUri;
            this.managementUri = managementUri;
        }

        public V1Deployment CreateDeployment(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            var deployment = this.PrepareDeployment(identity, module, labels);
            deployment.Metadata.Annotations[KubernetesConstants.CreationString] = JsonConvert.SerializeObject(deployment);

            return deployment;
        }

        public void UpdateDeployment(V1Deployment to, V1Deployment from)
        {
            to.Metadata.ResourceVersion = from.Metadata.ResourceVersion;
        }

        V1Deployment PrepareDeployment(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            string name = identity.DeploymentName();

            var podSpec = this.GetPod(name, identity, module, labels);

            var selector = new V1LabelSelector(matchLabels: labels);
            var deploymentSpec = new V1DeploymentSpec(replicas: 1, selector: selector, template: podSpec);

            var deploymentMeta = new V1ObjectMeta(name: name, labels: labels, annotations: new Dictionary<string, string>());
            return new V1Deployment(metadata: deploymentMeta, spec: deploymentSpec);
        }

        V1PodTemplateSpec GetPod(string name, IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            List<V1EnvVar> envVars = this.CollectEnv(module, identity);

            // Convert docker labels to annotations because docker labels don't have the same restrictions as Kubernetes labels.
            var annotations = Option.Maybe(module.Config.CreateOptions?.Labels)
                .Map(dockerLabels => dockerLabels.ToDictionary(label => KubeUtils.SanitizeAnnotationKey(label.Key), label => label.Value))
                .GetOrElse(() => new Dictionary<string, string>());
            annotations[KubernetesConstants.K8sEdgeOriginalModuleId] = ModuleIdentityHelper.GetModuleName(identity.ModuleId);

            // Per container settings:
            // exposed ports
            Option<List<V1ContainerPort>> exposedPorts = Option.Maybe(module.Config?.CreateOptions?.ExposedPorts)
                .Map(PortExtensions.GetContainerPorts);

            // privileged container
            Option<V1SecurityContext> securityContext = Option.Maybe(module.Config?.CreateOptions?.HostConfig?.Privileged)
                .Map(config => new V1SecurityContext(privileged: true));

            // Bind mounts
            (List<V1Volume> volumes, List<V1VolumeMount> proxyMounts, List<V1VolumeMount> volumeMounts) = this.GetVolumesFromModule(module);

            var containers = new List<V1Container>
            {
                new V1Container(
                    name,
                    env: envVars,
                    image: module.Config.Image,
                    volumeMounts: volumeMounts,
                    securityContext: securityContext.OrDefault(),
                    ports: exposedPorts.OrDefault()),

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

            var modulePodSpec = new V1PodSpec(containers, volumes: volumes, imagePullSecrets: imageSecret.OrDefault(), serviceAccountName: name);

            var objectMeta = new V1ObjectMeta(name: name, labels: labels, annotations: annotations);
            return new V1PodTemplateSpec(objectMeta, modulePodSpec);
        }

        List<V1EnvVar> CollectEnv(KubernetesModule module, IModuleIdentity identity)
        {
            var envList = module.Env.Select(env => new V1EnvVar(env.Key, env.Value.Value)).ToList();

            char[] envSplit = { '=' };
            if (module.Config?.CreateOptions?.Env != null)
            {
                foreach (string hostEnv in module.Config?.CreateOptions?.Env)
                {
                    string[] keyValue = hostEnv.Split(envSplit, 2);
                    if (keyValue.Length == 2)
                    {
                        envList.Add(new V1EnvVar(keyValue[0], keyValue[1]));
                    }
                }
            }

            envList.Add(new V1EnvVar(CoreConstants.IotHubHostnameVariableName, identity.IotHubHostname));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletAuthSchemeVariableName, "sasToken"));
            envList.Add(new V1EnvVar(Logger.RuntimeLogLevelEnvKey, Logger.GetLogLevel().ToString()));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletWorkloadUriVariableName, this.workloadUri.ToString()));
            if (identity.Credentials is IdentityProviderServiceCredentials creds)
            {
                envList.Add(new V1EnvVar(CoreConstants.EdgeletModuleGenerationIdVariableName, creds.ModuleGenerationId));
            }

            envList.Add(new V1EnvVar(CoreConstants.DeviceIdVariableName, identity.DeviceId));
            envList.Add(new V1EnvVar(CoreConstants.ModuleIdVariableName, identity.ModuleId));
            envList.Add(new V1EnvVar(CoreConstants.EdgeletApiVersionVariableName, this.workloadApiVersion));

            if (string.Equals(identity.ModuleId, CoreConstants.EdgeAgentModuleIdentityName))
            {
                envList.Add(new V1EnvVar(CoreConstants.ModeKey, CoreConstants.KubernetesMode));
                envList.Add(new V1EnvVar(CoreConstants.EdgeletManagementUriVariableName, this.managementUri.ToString()));
                envList.Add(new V1EnvVar(CoreConstants.NetworkIdKey, "azure-iot-edge"));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyImageEnvKey, this.proxyImage));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigPathEnvKey, this.proxyConfigPath));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigVolumeEnvKey, this.proxyConfigVolumeName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigMapNameEnvKey, this.proxyConfigMapName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundlePathEnvKey, this.proxyTrustBundlePath));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundleVolumeEnvKey, this.proxyTrustBundleVolumeName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundleConfigMapEnvKey, this.proxyTrustBundleConfigMapName));
                envList.Add(new V1EnvVar(KubernetesConstants.K8sNamespaceKey, this.deviceNamespace));
            }

            if (string.Equals(identity.ModuleId, CoreConstants.EdgeAgentModuleIdentityName) ||
                string.Equals(identity.ModuleId, CoreConstants.EdgeHubModuleIdentityName))
            {
                envList.Add(new V1EnvVar(CoreConstants.EdgeDeviceHostNameKey, this.edgeHostname));
            }
            else
            {
                envList.Add(new V1EnvVar(CoreConstants.GatewayHostnameVariableName, EdgeHubHostname));
            }

            return envList;
        }

        (List<V1Volume>, List<V1VolumeMount>, List<V1VolumeMount>) GetVolumesFromModule(IModule<AgentDocker.CombinedDockerConfig> moduleWithDockerConfig)
        {
            var volumeList = new List<V1Volume>
            {
                new V1Volume(this.proxyConfigVolumeName, configMap: new V1ConfigMapVolumeSource(name: this.proxyConfigMapName)),
                new V1Volume(this.proxyTrustBundleVolumeName, configMap: new V1ConfigMapVolumeSource(name: this.proxyTrustBundleConfigMapName))
            };

            var proxyMountList = new List<V1VolumeMount>
            {
                new V1VolumeMount(this.proxyConfigPath, this.proxyConfigVolumeName),
                new V1VolumeMount(this.proxyTrustBundlePath, this.proxyTrustBundleVolumeName)
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
                foreach (Mount mount in moduleWithDockerConfig.Config?.CreateOptions?.HostConfig?.Mounts)
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
