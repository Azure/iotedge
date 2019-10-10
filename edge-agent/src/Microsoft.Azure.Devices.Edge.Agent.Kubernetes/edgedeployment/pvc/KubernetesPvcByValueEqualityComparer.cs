// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class KubernetesPvcByValueEqualityComparer : IEqualityComparer<V1PersistentVolumeClaim>
    {
        const string Storage = "storage";
        static readonly DictionaryComparer<string, string> LabelComparer = DictionaryComparer.StringDictionaryComparer;

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

            // This should be set for all valid V1PersistentVolumeClaim in this application.
            // (Make this check here because SequenceEquals below requires non-null objects.)
            if (x.Spec?.AccessModes == null || y.Spec?.AccessModes == null)
            {
                return false;
            }

            // For storage class name and volume name, k8s api fills out the missing fields.
            // for equivalence here, either VolumeName or StorageClassName have to be the same.
            return x.Metadata?.Name == y.Metadata?.Name &&
                  LabelComparer.Equals(x.Metadata?.Labels, y.Metadata?.Labels) &&
                  (x.Spec?.VolumeName == y.Spec?.VolumeName ||
                  x.Spec?.StorageClassName == y.Spec?.StorageClassName) &&
                  x.Spec.AccessModes.SequenceEqual(y.Spec.AccessModes) &&
                  this.GetStorage(x) == this.GetStorage(y);
        }

        Option<ResourceQuantity> GetStorage(V1PersistentVolumeClaim claim) => Option.Maybe(claim.Spec?.Resources?.Requests).FlatMap(requests => requests.Get(Storage));

        public int GetHashCode(V1PersistentVolumeClaim obj)
        {
            unchecked
            {
                int hashCode = obj.Spec?.AccessModes != null ? obj.Spec.AccessModes.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (obj.Metadata?.Name != null ? obj.Metadata.Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Metadata?.Labels != null ? obj.Metadata.Labels.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Spec?.Resources != null ? obj.Spec.Resources.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Spec?.StorageClassName != null ? obj.Spec.StorageClassName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Spec?.VolumeName != null ? obj.Spec.VolumeName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
