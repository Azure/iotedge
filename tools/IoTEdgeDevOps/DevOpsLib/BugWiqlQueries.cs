// Copyright (c) Microsoft. All rights reserved.

namespace DevOpsLib
{
    public static class BugWiqlQueries
    {
        public const string PrioritizedBugTemplate =
@"SELECT
    [System.Id],
    [System.WorkItemType],
    [System.Title],
    [System.AssignedTo],
    [System.State],
    [System.Tags]
FROM workitems
WHERE
    [System.TeamProject] = @project
    AND [System.WorkItemType] = 'Bug'
    AND [System.AreaPath] = 'One\IoT\Platform and Devices\IoT Devices\{AREA}'
    AND NOT [System.State] CONTAINS 'Done'
    AND NOT [System.State] CONTAINS 'Removed'
    AND [Microsoft.VSTS.Common.Priority] = {PRIORITY}";

        public const string PrioritizedStartedBugTemplate =
@"SELECT
    [System.Id],
    [System.WorkItemType],
    [System.Title],
    [System.AssignedTo],
    [System.State],
    [System.Tags]
FROM workitems
WHERE
    [System.TeamProject] = @project
    AND [System.WorkItemType] = 'Bug'
    AND [System.AreaPath] = 'One\IoT\Platform and Devices\IoT Devices\{AREA}'
    AND NOT [System.State] CONTAINS 'Done'
    AND NOT [System.State] CONTAINS 'Removed'
    AND [Microsoft.VSTS.Common.Priority] = {PRIORITY}
    AND [System.State] = 'In Progress'";

        public const string UnprioritizedBugTemplate =
@"SELECT
    [System.Id],
    [System.WorkItemType],
    [System.Title],
    [System.AssignedTo],
    [System.State],
    [System.Tags]
FROM workitems
WHERE
    [System.TeamProject] = @project
    AND [System.WorkItemType] = 'Bug'
    AND [System.AreaPath] = 'One\IoT\Platform and Devices\IoT Devices\{AREA}'
    AND NOT [System.State] CONTAINS 'Done'
    AND NOT [System.State] CONTAINS 'Removed'
    AND NOT [Microsoft.VSTS.Common.Priority] IN (1)
    AND NOT [Microsoft.VSTS.Common.Priority] IN (2)";

        public const string UnprioritizedStartedBugTemplate =
@"SELECT
    [System.Id],
    [System.WorkItemType],
    [System.Title],
    [System.AssignedTo],
    [System.State],
    [System.Tags]
FROM workitems
WHERE
    [System.TeamProject] = @project
    AND [System.WorkItemType] = 'Bug'
    AND [System.AreaPath] = 'One\IoT\Platform and Devices\IoT Devices\{AREA}'
    AND NOT [System.State] CONTAINS 'Done'
    AND NOT [System.State] CONTAINS 'Removed'
    AND NOT [Microsoft.VSTS.Common.Priority] IN (1)
    AND NOT [Microsoft.VSTS.Common.Priority] IN (2)
    AND [System.State] = 'In Progress'";
    }
}