// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;
    public interface IDeploymentBackupSource
    {
        string Name { get; }

        Task<DeploymentConfigInfo> ReadFromBackupAsync();

        Task BackupDeploymentConfigAsync(DeploymentConfigInfo deploymentConfigInfo);
    }
}
