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
    using Serilog.Events;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesDeploymentMapper : IKubernetesDeploymentMapper
    {
        const string EdgeHubHostname = "edgehub";

        readonly string deviceNamespace;
        readonly string edgeHostname;
        readonly string proxyImage;
        readonly Option<string> proxyImagePullSecretName;
        readonly string proxyConfigPath;
        readonly string proxyConfigVolumeName;
        readonly string proxyConfigMapName;
        readonly string proxyTrustBundlePath;
        readonly string proxyTrustBundleVolumeName;
        readonly string proxyTrustBundleConfigMapName;
        readonly PortMapServiceType defaultServiceType;
        readonly Option<string> persistentVolumeName;
        readonly Option<string> storageClassName;
        readonly Option<uint> persistentVolumeClaimDefaultSizeMb;
        readonly string workloadApiVersion;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly bool runAsNonRoot;
        readonly bool enableServiceCallTracing;
        readonly IDictionary<string, bool> experimentalFeatures;

        public KubernetesDeploymentMapper(
            string deviceNamespace,
            string edgeHostname,
            string proxyImage,
            Option<string> proxyImagePullSecretName,
            string proxyConfigPath,
            string proxyConfigVolumeName,
            string proxyConfigMapName,
            string proxyTrustBundlePath,
            string proxyTrustBundleVolumeName,
            string proxyTrustBundleConfigMapName,
            PortMapServiceType defaultServiceType,
            string persistentVolumeName,
            string storageClassName,
            Option<uint> persistentVolumeClaimDefaultSizeMb,
            string workloadApiVersion,
            Uri workloadUri,
            Uri managementUri,
            bool runAsNonRoot,
            bool enableServiceCallTracing,
            IDictionary<string, bool> experimentalFeatures)
        {
            this.deviceNamespace = deviceNamespace;
            this.edgeHostname = edgeHostname;
            this.proxyImage = proxyImage;
            this.proxyImagePullSecretName = proxyImagePullSecretName;
            this.proxyConfigPath = proxyConfigPath;
            this.proxyConfigVolumeName = proxyConfigVolumeName;
            this.proxyConfigMapName = proxyConfigMapName;
            this.proxyTrustBundlePath = proxyTrustBundlePath;
            this.proxyTrustBundleVolumeName = proxyTrustBundleVolumeName;
            this.proxyTrustBundleConfigMapName = proxyTrustBundleConfigMapName;
            this.defaultServiceType = defaultServiceType;
            this.persistentVolumeName = Option.Maybe(persistentVolumeName)
                .Filter(p => !string.IsNullOrWhiteSpace(p));
            this.storageClassName = Option.Maybe(storageClassName);
            this.persistentVolumeClaimDefaultSizeMb = persistentVolumeClaimDefaultSizeMb;
            this.workloadApiVersion = workloadApiVersion;
            this.workloadUri = workloadUri;
            this.managementUri = managementUri;
            this.runAsNonRoot = runAsNonRoot;
            this.enableServiceCallTracing = enableServiceCallTracing;
            this.experimentalFeatures = experimentalFeatures;
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
            // Desired status in Deployment should only be Running or Stopped. Assume Running if not Stopped
            int replicas = (module.DesiredStatus != ModuleStatus.Stopped) ? 1 : 0;
            var deploymentSpec = new V1DeploymentSpec(replicas: replicas, selector: selector, template: podSpec);

            var deploymentMeta = new V1ObjectMeta(
                name: name,
                labels: labels,
                annotations: new Dictionary<string, string>(),
                ownerReferences: module.Owner.ToOwnerReferences());
            return new V1Deployment(metadata: deploymentMeta, spec: deploymentSpec);
        }

        V1PodTemplateSpec GetPod(string name, IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            // Convert docker labels to annotations because docker labels don't have the same restrictions as Kubernetes labels.
            Dictionary<string, string> annotations = module.Config.CreateOptions.Labels
                .Map(dockerLabels => dockerLabels.ToDictionary(label => KubeUtils.SanitizeAnnotationKey(label.Key), label => label.Value))
                .GetOrElse(() => new Dictionary<string, string>());
            annotations[KubernetesConstants.K8sEdgeOriginalModuleId] = ModuleIdentityHelper.GetModuleName(identity.ModuleId);

            var (proxyContainer, proxyVolumes) = this.PrepareProxyContainer(module);
            var (moduleContainer, moduleVolumes) = this.PrepareModuleContainer(name, identity, module);

            var imagePullSecrets = new List<Option<string>> { this.proxyImagePullSecretName, module.Config.AuthConfig.Map(auth => auth.Name) }
                .FilterMap()
                .Distinct()
                .Select(pullSecretName => new V1LocalObjectReference(pullSecretName))
                .ToList();

            V1PodSecurityContext securityContext = module.Config.CreateOptions.SecurityContext.GetOrElse(
                () => this.runAsNonRoot
                    ? new V1PodSecurityContext { RunAsNonRoot = true, RunAsUser = 1000 }
                    : null);

            return new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Name = name,
                    Labels = labels,
                    Annotations = annotations
                },
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container> { proxyContainer, moduleContainer },
                    Volumes = proxyVolumes.Concat(moduleVolumes).ToList(),
                    ImagePullSecrets = imagePullSecrets.Any() ? imagePullSecrets : null,
                    SecurityContext = securityContext,
                    ServiceAccountName = name,
                    NodeSelector = module.Config.CreateOptions.NodeSelector.OrDefault()
                }
            };
        }

        (V1Container, IReadOnlyList<V1Volume>) PrepareModuleContainer(string name, IModuleIdentity identity, KubernetesModule module)
        {
            List<V1EnvVar> env = this.CollectModuleEnv(module, identity);

            (List<V1Volume> volumes, List<V1VolumeMount> volumeMounts) = this.CollectModuleVolumes(module);

            Option<V1SecurityContext> securityContext = module.Config.CreateOptions.HostConfig
                .Filter(config => config.Privileged)
                .Map(config => new V1SecurityContext(privileged: true));

            Option<List<V1ContainerPort>> exposedPorts = module.Config.CreateOptions.ExposedPorts
                .Map(PortExtensions.GetContainerPorts);

            var container = new V1Container
            {
                Name = name,
                Env = env,
                Image = module.Config.Image,
                VolumeMounts = volumeMounts,
                SecurityContext = securityContext.OrDefault(),
                Ports = exposedPorts.OrDefault(),
                Resources = module.Config.CreateOptions.Resources.OrDefault()
            };

            return (container, volumes);
        }

        List<V1EnvVar> CollectModuleEnv(KubernetesModule module, IModuleIdentity identity)
        {
            var envList = module.Env.Select(env => new V1EnvVar(env.Key, env.Value.Value)).ToList();

            module.Config.CreateOptions.Env.Map(ParseEnv)
                .ForEach(hostEnv => envList.AddRange(hostEnv));

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
                this.proxyImagePullSecretName.ForEach(ips => envList.Add(new V1EnvVar(KubernetesConstants.ProxyImagePullSecretNameEnvKey, ips)));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigPathEnvKey, this.proxyConfigPath));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigVolumeEnvKey, this.proxyConfigVolumeName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyConfigMapNameEnvKey, this.proxyConfigMapName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundlePathEnvKey, this.proxyTrustBundlePath));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundleVolumeEnvKey, this.proxyTrustBundleVolumeName));
                envList.Add(new V1EnvVar(KubernetesConstants.ProxyTrustBundleConfigMapEnvKey, this.proxyTrustBundleConfigMapName));
                envList.Add(new V1EnvVar(KubernetesConstants.K8sNamespaceKey, this.deviceNamespace));
                envList.Add(new V1EnvVar(KubernetesConstants.RunAsNonRootKey, this.runAsNonRoot.ToString()));
                envList.Add(new V1EnvVar(KubernetesConstants.EdgeK8sObjectOwnerApiVersionKey, module.Owner.ApiVersion));
                envList.Add(new V1EnvVar(KubernetesConstants.EdgeK8sObjectOwnerKindKey, module.Owner.Kind));
                envList.Add(new V1EnvVar(KubernetesConstants.EdgeK8sObjectOwnerNameKey, module.Owner.Name));
                envList.Add(new V1EnvVar(KubernetesConstants.EdgeK8sObjectOwnerUidKey, module.Owner.Uid));
                envList.Add(new V1EnvVar(KubernetesConstants.PortMappingServiceType, this.defaultServiceType.ToString()));
                envList.Add(new V1EnvVar(KubernetesConstants.EnableK8sServiceCallTracingName, this.enableServiceCallTracing.ToString()));
                this.persistentVolumeName.ForEach(pvName => envList.Add(new V1EnvVar(KubernetesConstants.PersistentVolumeNameKey, pvName)));
                this.storageClassName.ForEach(scName => envList.Add(new V1EnvVar(KubernetesConstants.StorageClassNameKey, scName)));
                this.persistentVolumeClaimDefaultSizeMb.ForEach(size => envList.Add(new V1EnvVar(KubernetesConstants.PersistentVolumeClaimDefaultSizeInMbKey, size.ToString())));
                envList.AddRange(this.experimentalFeatures.Select(env => new V1EnvVar(env.Key, env.Value.ToString())));
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

        static IEnumerable<V1EnvVar> ParseEnv(IReadOnlyList<string> env) =>
            env.Select(hostEnv => hostEnv.Split('='))
                .Where(keyValue => keyValue.Length == 2)
                .Select(keyValue => new V1EnvVar(keyValue[0], keyValue[1]));

        (List<V1Volume>, List<V1VolumeMount>) CollectModuleVolumes(KubernetesModule module)
        {
            var volumeList = new List<V1Volume>();
            var volumeMountList = new List<V1VolumeMount>();

            // collect volumes and volume mounts from HostConfig.Binds section
            var binds = module.Config.CreateOptions.HostConfig
                .FlatMap(config => Option.Maybe(config.Binds))
                .Map(
                    hostBinds => hostBinds
                        .Select(bind => bind.Split(':'))
                        .Where(bind => bind.Length >= 2)
                        .Select(bind => new { Name = KubeUtils.SanitizeDNSValue(bind[0]), HostPath = bind[0], MountPath = bind[1], IsReadOnly = bind.Length > 2 && bind[2].Contains("ro") })
                        .ToList());

            binds.Map(hostBinds => hostBinds.Select(bind => new V1Volume(bind.Name, hostPath: new V1HostPathVolumeSource(bind.HostPath, "DirectoryOrCreate"))))
                .ForEach(volumes => volumeList.AddRange(volumes));

            binds.Map(hostBinds => hostBinds.Select(bind => new V1VolumeMount(bind.MountPath, bind.Name, readOnlyProperty: bind.IsReadOnly)))
                .ForEach(mounts => volumeMountList.AddRange(mounts));

            // collect volumes and volume mounts from HostConfig.Mounts section for binds to host path
            var bindMounts = module.Config.CreateOptions.HostConfig
                .FlatMap(config => Option.Maybe(config.Mounts))
                .Map(mounts => mounts.Where(mount => mount.Type.Equals("bind", StringComparison.InvariantCultureIgnoreCase)).ToList());

            bindMounts.Map(mounts => mounts.Select(mount => new V1Volume(KubeUtils.SanitizeDNSValue(mount.Source), hostPath: new V1HostPathVolumeSource(mount.Source, "DirectoryOrCreate"))))
                .ForEach(volumes => volumeList.AddRange(volumes));

            bindMounts.Map(mounts => mounts.Select(mount => new V1VolumeMount(mount.Target, KubeUtils.SanitizeDNSValue(mount.Source), readOnlyProperty: mount.ReadOnly)))
                .ForEach(mounts => volumeMountList.AddRange(mounts));

            // collect volumes and volume mounts from HostConfig.Mounts section for volumes via emptyDir
            var volumeMounts = module.Config.CreateOptions.HostConfig
                .FlatMap(config => Option.Maybe(config.Mounts))
                .Map(mounts => mounts.Where(mount => mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase)).ToList());

            volumeMounts.Map(mounts => mounts.Select(mount => this.GetVolume(module, mount)))
                .ForEach(volumes => volumeList.AddRange(volumes));

            volumeMounts.Map(mounts => mounts.Select(mount => new V1VolumeMount(mount.Target, KubeUtils.SanitizeDNSValue(mount.Source), readOnlyProperty: mount.ReadOnly)))
                .ForEach(mounts => volumeMountList.AddRange(mounts));

            // collect volume and volume mounts from CreateOption.Volumes section @kubernetes extended feature
            module.Config.CreateOptions.Volumes
                .Map(volumes => volumes.Select(volume => volume.Volume).FilterMap())
                .ForEach(volumes => volumeList.AddRange(volumes));

            module.Config.CreateOptions.Volumes
                .Map(volumes => volumes.Select(volume => volume.VolumeMounts).FilterMap())
                .ForEach(mounts => volumeMountList.AddRange(mounts.SelectMany(x => x)));

            return (volumeList, volumeMountList);
        }

        (V1Container, List<V1Volume>) PrepareProxyContainer(KubernetesModule module)
        {
            var env = new List<V1EnvVar>
            {
                new V1EnvVar("PROXY_LOG", ToProxyLogLevel(Logger.GetLogLevel()))
            };

            var volumeMounts = new List<V1VolumeMount>
            {
                new V1VolumeMount { MountPath = this.proxyConfigPath, Name = this.proxyConfigVolumeName },
                new V1VolumeMount { MountPath = this.proxyTrustBundlePath, Name = this.proxyTrustBundleVolumeName }
            };

            var volumes = new List<V1Volume>
            {
                new V1Volume { Name = this.proxyConfigVolumeName, ConfigMap = new V1ConfigMapVolumeSource(name: this.proxyConfigMapName) },
                new V1Volume { Name = this.proxyTrustBundleVolumeName, ConfigMap = new V1ConfigMapVolumeSource(name: this.proxyTrustBundleConfigMapName) }
            };

            var container = new V1Container
            {
                Name = "proxy",
                Env = env,
                Image = this.proxyImage,
                VolumeMounts = volumeMounts
            };

            return (container, volumes);
        }

        static readonly Dictionary<LogEventLevel, string> ProxyLogLevel = new Dictionary<LogEventLevel, string>
        {
            [LogEventLevel.Verbose] = "Trace",
            [LogEventLevel.Debug] = "Debug",
            [LogEventLevel.Information] = "Info",
            [LogEventLevel.Warning] = "Warn",
            [LogEventLevel.Error] = "Error",
            [LogEventLevel.Fatal] = "Error",
        };

        static string ToProxyLogLevel(LogEventLevel level)
        {
            if (!ProxyLogLevel.TryGetValue(level, out string proxyLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(level), $"Unknown log level: {level}");
            }

            return proxyLevel;
        }

        V1Volume GetVolume(KubernetesModule module, Mount mount)
        {
            string volumeName = KubeUtils.SanitizeK8sValue(mount.Source);
            string pvcName = KubernetesModule.PvcName(module, mount);

            // PVC name will be modulename + volume name
            // Volume name will be customer defined name or modulename + mount.source
            if (this.persistentVolumeName.HasValue)
            {
                string pvName = this.persistentVolumeName.OrDefault();

                if (pvName != volumeName)
                {
                    throw new InvalidModuleException(string.Format("The mount name {0} has to be the same as the PV name {1}", volumeName, pvName));
                }

                return new V1Volume
                {
                    Name = volumeName,
                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource(pvcName, mount.ReadOnly)
                };
            }

            if (this.storageClassName.HasValue)
            {
                return new V1Volume
                {
                    Name = volumeName,
                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource(pvcName, mount.ReadOnly)
                };
            }

            return new V1Volume
            {
                Name = volumeName,
                EmptyDir = new V1EmptyDirVolumeSource()
            };
        }
    }
}
