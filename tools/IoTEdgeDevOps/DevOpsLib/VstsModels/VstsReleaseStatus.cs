// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/releases/list?view=azure-devops-rest-5.1#releasestatus
    public enum VstsReleaseStatus
    {
        Abandoned,
        Active,
        Draft,
        Undefined
    }
}
