// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1#buildstatus
    public enum VstsBuildStatus
    {
        None,
        Cancelling,
        Completed,
        InProgress,
        NotStarted,
        Postponed
    }
}
