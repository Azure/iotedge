// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;

    public class BugQuery
    {
        public BugQuery(string area, BugPriorityGrouping priority, bool inProgress)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(area, nameof(area));
            ValidationUtil.ThrowIfNull(inProgress, nameof(inProgress));

            this.Area = area;
            this.Priority = priority;
            this.InProgress = inProgress;
        }

        public string Area { get; }
        public BugPriorityGrouping Priority { get; }
        public bool InProgress { get; }

        public string Title
        {
            get
            {
                string titleBase = $"{this.Area}-{BugPriorityExtension.DisplayName(this.Priority)}";

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

            if (this.Priority != BugPriorityGrouping.Other && !this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedBugTemplate;
            }
            else if (this.Priority != BugPriorityGrouping.Other && this.InProgress)
            {
                query = BugWiqlQueries.PrioritizedStartedBugTemplate;
            }
            else if (this.Priority == BugPriorityGrouping.Other && !this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedBugTemplate;
            }
            else if (this.Priority == BugPriorityGrouping.Other && this.InProgress)
            {
                query = BugWiqlQueries.UnprioritizedStartedBugTemplate;
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
