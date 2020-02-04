// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class KubernetesImagePullSecretBySecretDataEqualityComparer : IEqualityComparer<V1Secret>
    {
        static readonly DictionaryComparer<string, string> MetadataComparer = DictionaryComparer.StringDictionaryComparer;

        public bool Equals(V1Secret x, V1Secret y)
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

            if (x.Metadata.Name != y.Metadata.Name)
            {
                return false;
            }

            if (!MetadataComparer.Equals(x.Metadata.Labels, y.Metadata.Labels))
            {
                return false;
            }

            if (x.Type != y.Type)
            {
                return false;
            }

            if (x.Data == null && y.Data == null)
            {
                return true;
            }

            if (x.Data != null && y.Data != null)
            {
                if (x.Data.Count != y.Data.Count)
                {
                    return false;
                }

                if (x.Data.Keys.Intersect(y.Data.Keys).Count() != x.Data.Count)
                {
                    return false;
                }

                return x.Data.Keys.Any(key => x.Data[key].SequenceEqual(y.Data[key]));
            }

            return false;
        }

        public int GetHashCode(V1Secret obj) => obj.GetHashCode();
    }
}
