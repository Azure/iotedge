// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service
{
    using System.Collections.Generic;
    using k8s.Models;
    using Newtonsoft.Json;

    public sealed class KubernetesServiceByCreationStringEqualityComparer : IEqualityComparer<V1Service>
    {
        public bool Equals(V1Service x, V1Service y)
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

            string xCreationString = GetCreationString(x);
            string yCreationString = GetCreationString(y);

            return xCreationString == yCreationString;
        }

        static string GetCreationString(V1Service service)
        {
            if (service.Metadata?.Annotations == null || !service.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
            {
                var serviceWithoutStatus = new V1Service(service.ApiVersion, service.Kind, service.Metadata, service.Spec);
                creationString = JsonConvert.SerializeObject(serviceWithoutStatus);
            }

            return creationString;
        }

        public int GetHashCode(V1Service obj) => obj.GetHashCode();
    }
}
