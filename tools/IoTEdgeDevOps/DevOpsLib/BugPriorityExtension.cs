// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BugPriorityExtension
    {
        public static HashSet<BugPriority> BugPriorities => 
            new HashSet<BugPriority>()
            {
                BugPriority.One,
                BugPriority.Two,
                BugPriority.Other
            };
        static Dictionary<BugPriority, string> definitionIdToDisplayNameMapping = new Dictionary<BugPriority, string>
        {
            { BugPriority.One, "Pri-1" },
            { BugPriority.Two, "Pri-2" },
            { BugPriority.Other, "Pri-Other" },
        };

        static Dictionary<BugPriority, string> definitionIdToPriorityValue = new Dictionary<BugPriority, string>
        {
            { BugPriority.One, "1" },
            { BugPriority.Two, "2" },
        };

        public static string DisplayName(this BugPriority bugPriority)
        {
            return definitionIdToDisplayNameMapping.ContainsKey(bugPriority) ? definitionIdToDisplayNameMapping[bugPriority] : bugPriority.ToString();
        }

        public static string PriorityValue(this BugPriority bugPriority)
        {
            return definitionIdToPriorityValue.ContainsKey(bugPriority) ? definitionIdToPriorityValue[bugPriority] : bugPriority.ToString();
        }
    }
}

