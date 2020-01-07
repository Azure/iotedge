// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    public struct KubernetesEventIds
    {
        public const int KubernetesPlanner = EventIdStart + 100;
        public const int KubernetesCommand = EventIdStart + 200;
        public const int EdgeDeploymentOperator = EventIdStart + 300;
        public const int EdgeDeploymentController = EventIdStart + 400;
        public const int KubernetesEnvironmentOperator = EventIdStart + 500;
        public const int KubernetesExperimentalCreateOptions = EventIdStart + 600;
        public const int KubernetesModelValidation = EventIdStart + 700;
        public const int KubernetesServiceBuilder = EventIdStart + 800;
        const int EventIdStart = 200000;
    }
}
