// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/deployments/list?view=azure-devops-rest-5.1#deploymentstatus
    public enum VstsDeploymentStatus
    {
        Undefined,
        All,
        Failed,
        InProgress,
        NotDeployed,
        PartiallySucceeded,
        Succeeded,
    }
}
