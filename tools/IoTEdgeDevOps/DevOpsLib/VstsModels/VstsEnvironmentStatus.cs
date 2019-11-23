// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/releases/get%20release%20environment?view=azure-devops-rest-5.1#environmentstatus
    public enum VstsEnvironmentStatus
    {
        Canceled,
        InProgress,
        NotStarted,
        PartiallySucceeded,
        Queued,
        Rejected,
        Scheduled,
        Succeeded,
        Undefined
    }
}
