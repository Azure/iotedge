// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment.Pvc
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesPvcByValueComparerTest
    {
        static KubernetesPvcByValueEqualityComparer comparer = new KubernetesPvcByValueEqualityComparer();

        [Fact]
        public void ReferenceComparisonTest()
        {
            var pvc1 = new V1PersistentVolumeClaim();
            var pvc2 = pvc1;
            Assert.True(comparer.Equals(pvc1, pvc2));
            Assert.False(comparer.Equals(null, pvc2));
            Assert.False(comparer.Equals(pvc1, null));
        }

        [Fact]
        public void NoAccessModeTest()
        {
            var x = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
                        [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue("hostname")
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            var y = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
                        [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue("hostname")
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            Assert.False(comparer.Equals(x, y));
        }

        [Fact]
        public void FieldComparisonTests()
        {
            var x = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                        [KubernetesConstants.K8sEdgeHubNameLabel] = "hostname"
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    StorageClassName = "angela",
                    AccessModes = new List<string> { "ReadOnce" },
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            var y = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                        [KubernetesConstants.K8sEdgeHubNameLabel] = "hostname"
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    StorageClassName = "angela",
                    AccessModes = new List<string> { "ReadOnce" },
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            x.Metadata.Name = "pvc2";
            Assert.False(comparer.Equals(x, y));
            x.Metadata.Name = "pvc1";
            Assert.True(comparer.Equals(x, y));

            x.Metadata.Labels[KubernetesConstants.K8sEdgeDeviceLabel] = "device2";
            Assert.False(comparer.Equals(x, y));
            x.Metadata.Labels[KubernetesConstants.K8sEdgeDeviceLabel] = "device1";
            Assert.True(comparer.Equals(x, y));

            // Match Either VolumeName or StorageClassName
            x.Spec.VolumeName = "artemus";
            Assert.True(comparer.Equals(x, y));
            x.Spec.VolumeName = "steve";
            x.Spec.StorageClassName = "dakota";
            Assert.True(comparer.Equals(x, y));

            // Both VolumeName and StorageClassName differ
            x.Spec.VolumeName = "artemus";
            x.Spec.StorageClassName = "dakota";
            Assert.False(comparer.Equals(x, y));
            x.Spec.VolumeName = "steve";
            x.Spec.StorageClassName = "angela";
            Assert.True(comparer.Equals(x, y));

            x.Spec.Resources.Requests = null;
            Assert.False(comparer.Equals(x, y));
            x.Spec.Resources.Requests = new Dictionary<string, ResourceQuantity>
            {
                ["storage"] = new ResourceQuantity("10M")
            };
            Assert.True(comparer.Equals(x, y));

            x.Spec.AccessModes = new List<string> { "ReadOnlyMany" };
            Assert.False(comparer.Equals(x, y));
            x.Spec.AccessModes = new List<string> { "ReadOnce" };
            Assert.True(comparer.Equals(x, y));
        }

        [Fact]
        public void SuccessComparisonTest()
        {
            var x = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                        [KubernetesConstants.K8sEdgeHubNameLabel] = "hostname"
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    AccessModes = new List<string> { "ReadOnce" },
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            var y = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: "pvc1",
                    labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = "device1",
                        [KubernetesConstants.K8sEdgeHubNameLabel] = "hostname"
                    }),
                Spec = new V1PersistentVolumeClaimSpec
                {
                    VolumeName = "steve",
                    AccessModes = new List<string> { "ReadOnce" },
                    Resources = new V1ResourceRequirements { Requests = new Dictionary<string, ResourceQuantity> { ["storage"] = new ResourceQuantity("10M") } }
                }
            };
            Assert.True(comparer.Equals(x, y));
        }
    }
}
