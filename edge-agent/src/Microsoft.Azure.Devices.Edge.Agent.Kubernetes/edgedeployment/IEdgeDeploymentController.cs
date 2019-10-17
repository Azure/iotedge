// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IEdgeDeploymentController
    {
        Task<ModuleSet> DeployModulesAsync(IReadOnlyList<KubernetesModule> modules, ModuleSet currentModules);

        Task PurgeModulesAsync();
    }
}
