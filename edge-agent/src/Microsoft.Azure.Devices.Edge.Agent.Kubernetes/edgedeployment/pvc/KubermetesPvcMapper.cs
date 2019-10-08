// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesPvcMapper : IKubernetesPvcMapper
    {
        readonly Option<string> persistentVolumeName;
        readonly Option<string> storageClassName;
        readonly uint persistentVolumeClaimSizeMb;

        public KubernetesPvcMapper(
            string persistentVolumeName,
            string storageClassName,
            uint persistentVolumeClaimSizeMb)
        {
            this.persistentVolumeName = Option.Maybe(persistentVolumeName)
                .Filter(p => !string.IsNullOrWhiteSpace(p));
            this.storageClassName = Option.Maybe(storageClassName);
            this.persistentVolumeClaimSizeMb = persistentVolumeClaimSizeMb;
        }

        public Option<List<V1PersistentVolumeClaim>> CreatePersistentVolumeClaims(KubernetesModule module, IDictionary<string, string> labels) =>
            module.Config.CreateOptions.HostConfig
                .FlatMap(hostConfig => Option.Maybe(hostConfig.Mounts))
                .Map(mounts => mounts.Where(this.ShouldCreatePvc).Select(mount => this.ExtractPvc(mount, labels)).ToList())
                .Filter(mounts => mounts.Any());

        bool ShouldCreatePvc(Mount mount)
        {
            if (!mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return this.storageClassName.HasValue || this.persistentVolumeName.HasValue;
        }

        V1PersistentVolumeClaim ExtractPvc(Mount mount, IDictionary<string, string> labels)
        {
            string name = KubeUtils.SanitizeK8sValue(mount.Source);
            bool readOnly = mount.ReadOnly;
            var persistentVolumeClaimSpec = new V1PersistentVolumeClaimSpec()
            {
                AccessModes = new List<string> { readOnly ? "ReadOnlyMany" : "ReadWriteMany" },
                Resources = new V1ResourceRequirements()
                {
                    Requests = new Dictionary<string, ResourceQuantity>() { { "storage", new ResourceQuantity($"{this.persistentVolumeClaimSizeMb}Mi") } }
                },
            };
            // prefer persistent volume name to storage class name, if both are set.
            if (this.persistentVolumeName.HasValue)
            {
                this.persistentVolumeName.ForEach(volumeName => persistentVolumeClaimSpec.VolumeName = volumeName);
            }
            else if (this.storageClassName.HasValue)
            {
                this.storageClassName.ForEach(storageClass => persistentVolumeClaimSpec.StorageClassName = storageClass);
            }

            return new V1PersistentVolumeClaim(metadata: new V1ObjectMeta(name: name, labels: labels), spec: persistentVolumeClaimSpec);
        }

        public void UpdatePersistentVolumeClaim(V1PersistentVolumeClaim to, V1PersistentVolumeClaim from)
        {
            // TODO: ReadWriteOnce is the Kubernetes moniker for single pod use only
            //       What should we do if this is the 2nd module to make this claim?
            to.Metadata.ResourceVersion = from.Metadata.ResourceVersion;
        }
    }
}
