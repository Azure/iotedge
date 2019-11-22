// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;

    public static class Constants
    {
        public static class EdgeDeployment
        {
            public const string ApiVersion = Api + "/" + Version;

            public const string Api = "microsoft.azure.devices.edge";

            public const string Version = "v1";

            public const string Kind = "EdgeDeployment";

            public const string Group = "microsoft.azure.devices.edge";

            public const string Plural = "edgedeployments";
        }

        public const string CreationString = "net.azure-devices.edge.creationstring";

        public const string DefaultDeletePropagationPolicy = "Background";

        public const PortMapServiceType DefaultPortMapServiceType = PortMapServiceType.ClusterIP;

        public const string K8sEdgeModuleLabel = "net.azure-devices.edge.module";

        public const string K8sEdgeOriginalModuleId = "net.azure-devices.edge.original-moduleid";

        public const string K8sEdgeDeviceLabel = "net.azure-devices.edge.deviceid";

        public const string K8sEdgeHubNameLabel = "net.azure-devices.edge.hub";

        public const string K8sNameDivider = "-";

        public const string K8sPullSecretType = "kubernetes.io/dockerconfigjson";

        public const string K8sPullSecretData = ".dockerconfigjson";

        public const string PortMappingServiceType = "PortMappingServiceType";

        public const string EnableK8sServiceCallTracingName = "EnableK8sServiceCallTracing";

        public const string K8sNamespaceKey = "K8sNamespace";

        public const string ProxyImageEnvKey = "ProxyImage";

        public const string ProxyImagePullSecretNameEnvKey = "ProxyImagePullSecretName";

        public const string ProxyConfigPathEnvKey = "ProxyConfigPath";

        public const string ProxyConfigVolumeEnvKey = "ProxyConfigVolume";

        public const string ProxyConfigMapNameEnvKey = "ProxyConfigMapName";

        public const string ProxyTrustBundlePathEnvKey = "ProxyTrustBundlePath";

        public const string ProxyTrustBundleVolumeEnvKey = "ProxyTrustBundleVolume";

        public const string ProxyTrustBundleConfigMapEnvKey = "ProxyTrustBundleConfigMapName";

        public const string PersistentVolumeNameKey = "PersistentVolumeName";

        public const string StorageClassNameKey = "StorageClassName";

        public const string PersistentVolumeClaimDefaultSizeInMbKey = "PersistentVolumeClaimDefaultSizeInMb";

        public const string EdgeK8sObjectOwnerApiVersionKey = "EdgeK8sObjectOwnerApiVersion";

        public const string EdgeK8sObjectOwnerKindKey = "EdgeK8sObjectOwnerKind";

        public const string EdgeK8sObjectOwnerNameKey = "EdgeK8sObjectOwnerName";

        public const string EdgeK8sObjectOwnerUidKey = "EdgeK8sObjectOwnerUid";

        public const string RunAsNonRootKey = "RunAsNonRoot";
    }
}
