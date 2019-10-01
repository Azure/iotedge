// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Newtonsoft.Json;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    public sealed class KubernetesDeploymentByCreationStringEqualityComparer : IEqualityComparer<V1Deployment>
    {
        public bool Equals(V1Deployment x, V1Deployment y)
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

            // EdgeAgent deployments are equal when they have identical image sections
            if (x.Metadata.Name == KubeUtils.SanitizeK8sValue(CoreConstants.EdgeAgentModuleName))
            {
                return V1DeploymentEx.ImageEquals(x, y);
            }

            // compares by creation string
            string xCreationString = GetCreationString(x);
            string yCreationString = GetCreationString(y);

            return xCreationString == yCreationString;
        }

        static string GetCreationString(V1Deployment deployment)
        {
            if (deployment.Metadata?.Annotations == null || !deployment.Metadata.Annotations.TryGetValue(Constants.CreationString, out string creationString))
            {
                var deploymentWithoutStatus = new V1Deployment(deployment.ApiVersion, deployment.Kind, deployment.Metadata, deployment.Spec);
                creationString = JsonConvert.SerializeObject(deploymentWithoutStatus);
            }

            return creationString;
        }

        public int GetHashCode(V1Deployment obj) => obj.GetHashCode();
    }
}
