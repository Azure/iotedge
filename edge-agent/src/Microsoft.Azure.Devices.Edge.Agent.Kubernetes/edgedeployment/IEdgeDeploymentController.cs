// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IEdgeDeploymentController
    {
        Task<EdgeDeploymentStatus> DeployModulesAsync(ModuleSet modules, ModuleSet currentModules);
    }
}
