// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BugPriorityExtension
    {
        public static HashSet<BugPriorityGrouping> BugPriorities =>
            new HashSet<BugPriorityGrouping>()
            {
                BugPriorityGrouping.One,
                BugPriorityGrouping.Two,
                BugPriorityGrouping.Other
            };
        static Dictionary<BugPriorityGrouping, string> definitionIdToDisplayNameMapping = new Dictionary<BugPriorityGrouping, string>
        {
            { BugPriorityGrouping.One, "Pri-1" },
            { BugPriorityGrouping.Two, "Pri-2" },
            { BugPriorityGrouping.Other, "Pri-Other" },
        };

        static Dictionary<BugPriorityGrouping, string> definitionIdToPriorityValue = new Dictionary<BugPriorityGrouping, string>
        {
            { BugPriorityGrouping.One, "1" },
            { BugPriorityGrouping.Two, "2" },
        };

        public static string DisplayName(this BugPriorityGrouping bugPriority)
        {
            return definitionIdToDisplayNameMapping.ContainsKey(bugPriority) ? definitionIdToDisplayNameMapping[bugPriority] : bugPriority.ToString();
        }

        public static string PriorityValue(this BugPriorityGrouping bugPriority)
        {
            return definitionIdToPriorityValue.ContainsKey(bugPriority) ? definitionIdToPriorityValue[bugPriority] : bugPriority.ToString();
        }
    }
}
