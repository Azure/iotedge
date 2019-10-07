// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    enum PvcOption
    {
        None,
        VolumeName,
        StorageClass
    }

    public class KubernetesPvcMapper : IKubernetesPvcMapper
    {
        readonly string persistentVolumeName;
        readonly string storageClassName;
        readonly uint persistentVolumeClaimSizeMb;
        readonly PvcOption pvcOption;

        public KubernetesPvcMapper(
            string persistentVolumeName,
            string storageClassName,
            uint persistentVolumeClaimSizeMb)
        {
            this.persistentVolumeName = persistentVolumeName;
            this.storageClassName = storageClassName;
            this.persistentVolumeClaimSizeMb = persistentVolumeClaimSizeMb;
            // prefer persistent volume name to storage class name, if both are set.
            if (!string.IsNullOrWhiteSpace(persistentVolumeName))
            {
                this.pvcOption = PvcOption.VolumeName;
            }
            else if (!string.IsNullOrWhiteSpace(storageClassName))
            {
                this.pvcOption = PvcOption.StorageClass;
            }
            else
            {
                this.pvcOption = PvcOption.None;
            }
        }

        public Option<List<V1PersistentVolumeClaim>> CreatePersistentVolumeClaims(KubernetesModule module, IDictionary<string, string> labels) =>
            Option.Maybe(module.Config?.CreateOptions?.HostConfig?.Mounts)
                .Map(mounts => mounts.Select(mount => this.ExtractPvc(mount, labels)).FilterMap().ToList());

        Option<V1PersistentVolumeClaim> ExtractPvc(Mount mount, IDictionary<string, string> labels)
        {
            if (!mount.Type.Equals("volume", StringComparison.InvariantCultureIgnoreCase) ||
                this.pvcOption == PvcOption.None)
            {
                return Option.None<V1PersistentVolumeClaim>();
            }

            string name = KubeUtils.SanitizeK8sValue(mount.Source);
            bool readOnly = mount.ReadOnly;
            var persistentVolumeClaimSpec = new V1PersistentVolumeClaimSpec()
            {
                AccessModes = new List<string> { readOnly ? "ReadOnlyMany" : "ReadWriteMany" },
                Resources = new V1ResourceRequirements()
                {
                    Requests = new Dictionary<string, ResourceQuantity>() { { "storage", new ResourceQuantity($"{this.persistentVolumeClaimSizeMb}Mi") } }
                }
            };
            switch (this.pvcOption)
            {
                case PvcOption.VolumeName:
                    persistentVolumeClaimSpec.VolumeName = this.persistentVolumeName;
                    break;
                case PvcOption.StorageClass:
                    persistentVolumeClaimSpec.StorageClassName = this.storageClassName;
                    break;
                case PvcOption.None:
                default:
                    return Option.None<V1PersistentVolumeClaim>();
            }

            return Option.Maybe(new V1PersistentVolumeClaim(metadata: new V1ObjectMeta(name: name, labels: labels), spec: persistentVolumeClaimSpec));
        }

        public void UpdatePersistentVolumeClaim(V1PersistentVolumeClaim to, V1PersistentVolumeClaim from)
        {
            // TODO: ReadWriteOnce is the Kubernetes moniker for single pod use only
            //       What should we do if this is the 2nd module to make this claim?
            to.Metadata.ResourceVersion = from.Metadata.ResourceVersion;
        }
    }
}
