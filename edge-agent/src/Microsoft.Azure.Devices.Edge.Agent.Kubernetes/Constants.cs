// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    public static class Constants
    {
        public const string k8sApi = "microsoft.azure.devices.edge";

        public const string k8sApiVersion = "v1beta1";

        public const string k8sCrdKind = "EdgeDeployment";

        public const string k8sCrdGroup = "microsoft.azure.devices.edge";

        public const string k8sCrdPlural = "edgedeployments";

        public const string k8sNamespace = "microsoft-azure-devices-edge";

        public const string k8sEdgeModuleLabel = "net.azure-devices.edge.module";

        public const string k8sEdgeOriginalModuleId = "net.azure-devices.edge.original-moduleid";

        public const string k8sEdgeDeviceLabel = "net.azure-devices.edge.deviceid";

        public const string k8sEdgeHubNameLabel = "net.azure-devices.edge.hub";

        public const string CreationString = "net.azure-devices.edge.creationstring";

        public const string k8sNameDivider = "-";

        public const string k8sPullSecretType = "kubernetes.io/dockerconfigjson";

        public const string k8sPullSecretData = ".dockerconfigjson";

        public const string proxyImage = "darobs/envoy:0.1";

        public const string AgentConfigMap = "edgeagentconfigmap";

        public const string ModuleConfigMap = "moduleconfigmap";

    }
}
