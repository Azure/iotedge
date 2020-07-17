// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.EdgeDeployment.ServiceAccount
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesServiceAccountByValueComparerTest
    {
        static readonly KubernetesServiceAccountByValueEqualityComparer Comparer = new KubernetesServiceAccountByValueEqualityComparer();

        [Fact]
        public void ReferenceComparisonTest()
        {
            var pvc1 = new V1ServiceAccount();
            var pvc2 = pvc1;
            Assert.True(Comparer.Equals(pvc1, pvc2));
            Assert.False(Comparer.Equals(null, pvc2));
            Assert.False(Comparer.Equals(pvc1, null));
        }

        [Fact]
        public void ReturnsFalseIfFieldsAreDifferent()
        {
            var x = new V1ServiceAccount
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
            var y = new V1ServiceAccount
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
            Assert.True(Comparer.Equals(x, y));

            y.Metadata.Name = "object2";
            Assert.False(Comparer.Equals(x, y));

            y.Metadata.Name = "object1";
            Assert.True(Comparer.Equals(x, y));

            y.Metadata.Annotations = new Dictionary<string, string>
            {
                ["newkey"] = "Object1"
            };
            Assert.False(Comparer.Equals(x, y));

            y.Metadata.Annotations = new Dictionary<string, string>
            {
                [KubernetesConstants.K8sEdgeOriginalModuleId] = "Object1"
            };
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
            var saWithOwnerRef = new V1ServiceAccount
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
            var saWithoutOwnerRef = new V1ServiceAccount
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
