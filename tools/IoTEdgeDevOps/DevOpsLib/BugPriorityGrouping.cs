// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;

    public class BugPriorityGrouping : IEquatable<BugPriorityGrouping>
    {
        internal static readonly BugPriorityGrouping Pri0 = new BugPriorityGrouping("Pri-0", "0");
        internal static readonly BugPriorityGrouping Pri1 = new BugPriorityGrouping("Pri-1", "1");
        internal static readonly BugPriorityGrouping Pri2 = new BugPriorityGrouping("Pri-2", "2");
        internal static readonly BugPriorityGrouping PriOther = new BugPriorityGrouping("Pri-Other", "Other");

        BugPriorityGrouping(string name, string priority)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNullOrWhiteSpace(priority, nameof(priority));

            this.Name = name;
            this.Priority = priority;
        }

        public string Name { get; }
        public string Priority { get; }

        public bool Equals(BugPriorityGrouping bugPriorityGrouping)
        {
            return this.Name.Equals(bugPriorityGrouping.Priority) && this.Priority.Equals(bugPriorityGrouping.Priority);
        }
    }
}