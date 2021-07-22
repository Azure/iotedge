// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;

    public class BugWiqlQuery
    {
        public BugWiqlQuery(string area, BugPriorityGrouping bugPriorityGrouping, bool inProgress)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(area, nameof(area));
            ValidationUtil.ThrowIfNull(bugPriorityGrouping, nameof(bugPriorityGrouping));
            ValidationUtil.ThrowIfNull(inProgress, nameof(inProgress));

            this.Area = area;
            this.BugPriorityGrouping = bugPriorityGrouping;
            this.InProgress = inProgress;
        }

        public string Area { get; }
        public BugPriorityGrouping BugPriorityGrouping { get; }
        public bool InProgress { get; }

        public string Title
        {
            get
            {
                string titleBase = $"{this.Area}-{this.BugPriorityGrouping.Name}";

                if (this.InProgress)
                {
                    return $"{titleBase}-Started";
                }
                else
                {
                    return $"{titleBase}-Not-Started";
                }
            }
        }

        public string GetWiqlFromConfiguration()
        {
            string query;
            string unassignedPriorityPlaceholder = "Other";

            if (this.BugPriorityGrouping.Priority != unassignedPriorityPlaceholder && !this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedBugTemplate;
            }
            else if (this.BugPriorityGrouping.Priority != unassignedPriorityPlaceholder && this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedStartedBugTemplate;
            }
            else if (this.BugPriorityGrouping.Priority == unassignedPriorityPlaceholder && !this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedBugTemplate;
            }
            else if (this.BugPriorityGrouping.Priority == unassignedPriorityPlaceholder && this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedStartedBugTemplate;
            }
            else
            {
                throw new NotImplementedException();
            }

            query = query.Replace("{PRIORITY}", this.BugPriorityGrouping.Priority.ToString());
            query = query.Replace("{AREA}", this.Area);

            return query;
        }
    }
}
