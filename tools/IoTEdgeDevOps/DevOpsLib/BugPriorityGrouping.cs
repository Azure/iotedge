// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;

    public class BugPriorityGrouping : IEquatable<BugPriorityGrouping>
    {
        BugPriorityGrouping(string name, string priority)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNullOrWhiteSpace(priority, nameof(priority));

            this.Name = name;
            this.Priority = priority;
        }

        public string Name { get; }
        public string Priority { get; }

        public static readonly BugPriorityGrouping Pri1 = new BugPriorityGrouping("Pri-1", "1");
        public static readonly BugPriorityGrouping Pri2 = new BugPriorityGrouping("Pri-2", "2");
        public static readonly BugPriorityGrouping PriOther = new BugPriorityGrouping("Pri-Other", "Other");

        public bool Equals(BugPriorityGrouping bugPriorityGrouping)
        {
            return this.Name == bugPriorityGrouping.Name && this.Priority == bugPriorityGrouping.Priority;
        }
    }
}