// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    public static class Constants
    {
        public const string K8sApi = "microsoft.azure.devices.edge";

        public const string K8sApiVersion = "v1beta1";

        public const string K8sCrdKind = "EdgeDeployment";

        public const string K8sCrdGroup = "microsoft.azure.devices.edge";

        public const string K8sCrdPlural = "edgedeployments";

        public const string K8sNamespace = "microsoft-azure-devices-edge";

        public const string K8sNamespaceBaseName = "K8sNamespaceBaseName";

        public const string K8sEdgeModuleLabel = "net.azure-devices.edge.module";

        public const string K8sEdgeOriginalModuleId = "net.azure-devices.edge.original-moduleid";

        public const string K8sEdgeDeviceLabel = "net.azure-devices.edge.deviceid";

        public const string K8sEdgeHubNameLabel = "net.azure-devices.edge.hub";

        public const string CreationString = "net.azure-devices.edge.creationstring";

        public const string K8sNameDivider = "-";

        public const string K8sPullSecretType = "kubernetes.io/dockerconfigjson";

        public const string K8sPullSecretData = ".dockerconfigjson";

        public const string ProxyImage = "darobs/envoy:0.1";

        public const string AgentConfigMap = "edgeagentconfigmap";

        public const string ModuleConfigMap = "moduleconfigmap";

        public const PortMapServiceType DefaultPortMapServiceType = PortMapServiceType.ClusterIP;
    }
}
