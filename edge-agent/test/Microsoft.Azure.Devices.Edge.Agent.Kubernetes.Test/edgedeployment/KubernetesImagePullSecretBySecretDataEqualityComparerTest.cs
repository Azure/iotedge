// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesImagePullSecretBySecretDataEqualityComparerTest
    {
        static readonly KubernetesImagePullSecretBySecretDataEqualityComparer Comparer = new KubernetesImagePullSecretBySecretDataEqualityComparer();

        [Fact]
        public void ReferenceComparisonTest()
        {
            var secret1 = new V1Secret();
            var secret2 = secret1;

            Assert.True(Comparer.Equals(secret1, secret2));

            Assert.False(Comparer.Equals(null, secret2));
            Assert.False(Comparer.Equals(secret1, null));
        }

        [Fact]
        public void ReturnsFalseIfFieldsAreDifferent()
        {
            var x = new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Object1"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                    },
                    Name = "object1"
                },
                Data = new Dictionary<string, byte[]>
                {
                    [KubernetesConstants.K8sPullSecretData] = new byte[] { 1 }
                }
            };
            var y = new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Object1"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                    },
                    Name = "object1"
                },
                Data = new Dictionary<string, byte[]>
                {
                    [KubernetesConstants.K8sPullSecretData] = new byte[] { 1 }
                }
            };
            Assert.True(Comparer.Equals(x, y));

            y.Metadata.Name = "object2";
            Assert.False(Comparer.Equals(x, y));

            y.Metadata.Name = "object1";
            Assert.True(Comparer.Equals(x, y));

            y.Metadata.Labels = new Dictionary<string, string>
            {
                ["newkey2"] = "Object1"
            };
            Assert.False(Comparer.Equals(x, y));
        }

        [Fact]
        public void IgnoreOtherMetadataTest()
        {
            var saWithOwnerRef = new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Object1"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                    },
                    Name = "object1",
                    OwnerReferences = new List<V1OwnerReference>
                    {
                        new V1OwnerReference("v1", name: "iotedged", kind: "Deployment", uid: "123")
                    }
                }
            };
            var saWithoutOwnerRef = new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeOriginalModuleId] = "Object1"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                    },
                    Name = "object1"
                }
            };
            Assert.True(Comparer.Equals(saWithOwnerRef, saWithoutOwnerRef));
        }
    }
}
