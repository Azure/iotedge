// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public static class ModuleIdentityExtensions
    {
        public static string DeploymentName(this IModuleIdentity identity) => KubeUtils.SanitizeK8sValue(identity.ModuleId);
    }
}
