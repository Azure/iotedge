// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class KubernetesServiceAccountByValueEqualityComparer : IEqualityComparer<V1ServiceAccount>
    {
        static readonly DictionaryComparer<string, string> MetadataComparer = DictionaryComparer.StringDictionaryComparer;

        public bool Equals(V1ServiceAccount x, V1ServiceAccount y)
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

            return x.Metadata.Name == y.Metadata.Name &&
                   MetadataComparer.Equals(x.Metadata.Labels, y.Metadata.Labels) &&
                   MetadataComparer.Equals(x.Metadata.Annotations, y.Metadata.Annotations);
        }

        public int GetHashCode(V1ServiceAccount obj)
        {
            unchecked
            {
                int hashCode = obj.Metadata?.Name != null ? obj.Metadata.Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (obj.Metadata?.Labels != null ? obj.Metadata.Labels.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (obj.Metadata?.Annotations != null ? obj.Metadata.Annotations.GetHashCode() : 0);

                return hashCode;
            }
        }
    }
}
