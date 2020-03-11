// Copyright (c) Microsoft. All rights reserved.
using System;

namespace DevOpsLib
{
    public class BugQuery
    {
        public BugQuery(string title, string area, BugPriority priority, bool inProgress)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(title, nameof(title));
            ValidationUtil.ThrowIfNullOrWhiteSpace(area, nameof(area));
            ValidationUtil.ThrowIfNull(inProgress, nameof(inProgress));

            this.Title = title;
            this.Area = area;
            this.Priority = priority;
            this.InProgress = inProgress;
        }
        public string Title { get; }
        public string Area { get; }
        public BugPriority Priority { get; }
        public bool InProgress { get; }

        public string GetWiqlFromConfiguration()
        {
            string query = "";

            if (this.Priority != BugPriority.Other && !this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedBugTemplate ;
            }
            else if (this.Priority != BugPriority.Other && this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedStartedBugTemplate ;
            }
            else if (this.Priority == BugPriority.Other && !this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedBugTemplate ;
            }
            else if (this.Priority == BugPriority.Other && this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedStartedBugTemplate ;
            }
            else
            {
                throw new NotImplementedException();
            }

            query = query.Replace("{PRIORITY}", this.Priority.PriorityValue());
            query = query.Replace("{AREA}", this.Area);

            return query;
        }
    }
}
