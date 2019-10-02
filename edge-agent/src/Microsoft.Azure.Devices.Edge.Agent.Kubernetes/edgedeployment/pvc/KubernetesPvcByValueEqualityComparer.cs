// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class KubernetesPvcByValueEqualityComparer : IEqualityComparer<V1PersistentVolumeClaim>
    {
        const string Storage = "storage";
        static readonly DictionaryComparer<string, string> labelComparer = DictionaryComparer.StringDictionaryComparer;

        public bool Equals(V1PersistentVolumeClaim x, V1PersistentVolumeClaim y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.Metadata?.Name == y.Metadata?.Name &&
                   labelComparer.Equals(x.Metadata?.Labels, y.Metadata?.Labels) &&
                   x.Spec?.VolumeName == y.Spec?.VolumeName &&
                   x.Spec?.StorageClassName == y.Spec?.StorageClassName &&
                   x.Spec?.AccessModes == y.Spec?.AccessModes &&
                   this.GetStorage(x) == this.GetStorage(y);
        }

        Option<ResourceQuantity> GetStorage(V1PersistentVolumeClaim claim)
        {
            ResourceQuantity storage;
            if (claim.Spec?.Resources?.Requests != null && claim.Spec.Resources.Requests.TryGetValue(Storage, out storage))
            {
                return Option.Maybe(storage);
            }
            else
            {
                return Option.None<ResourceQuantity>();
            }
        }

        public int GetHashCode(V1PersistentVolumeClaim obj) => obj.GetHashCode();
    }
}
