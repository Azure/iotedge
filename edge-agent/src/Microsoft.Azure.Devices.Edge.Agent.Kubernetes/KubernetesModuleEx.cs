// Copyright (c) Microsoft. All rights reserved.


namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;

    public static class V1PodSpecEx
    {
        public static bool PodSpecEquals(V1PodSpec self, V1PodSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(self, other))
            {
                return true;
            }

            List<V1Container> otherList = other.Containers.ToList();

            foreach (V1Container selfContainer in self.Containers)
            {
                if (otherList.Exists(c => string.Equals(c.Name, selfContainer.Name)))
                {
                    V1Container otherContainer = otherList.Find(c => string.Equals(c.Name, selfContainer.Name));
                    if (!string.Equals(selfContainer.Image, otherContainer.Image))
                    {
                        // Container has a new image name.
                        return false;
                    }
                }
                else
                {
                    // container names don't match
                    return false;
                }
            }
            return true;
        }
    }

    public static class V1PodTemplateSpecEx
    {
        public static bool ImageEquals(V1PodTemplateSpec self, V1PodTemplateSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(self, other))
            {
                return true;
            }
            return V1PodSpecEx.PodSpecEquals(self.Spec, other.Spec);
        }
    }

    public static class V1DeploymentSpecEx
    {
        public static bool ImageEquals(V1DeploymentSpec self, V1DeploymentSpec other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(self, other))
            {
                return true;
            }
            return V1PodTemplateSpecEx.ImageEquals(self.Template, other.Template);
        }
    }
    public static class V1DeploymentEx
    {
        public static bool ImageEquals(V1Deployment self, V1Deployment other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(self, other))
            {
                return true;
            }

            return string.Equals(self.Kind, other.Kind) &&
                V1DeploymentSpecEx.ImageEquals(self.Spec, other.Spec);
        }
    }
}
