// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;

namespace DevOpsLib
{
    public static class BugQueryGenerator
    {
        static readonly string[] areas = new string[]
        {
            "IoTEdge",
            "IoTEdge\\AppModel",
            "IoTEdge\\AppModel\\K8s",
            "IoTEdge\\Connectivity",
            "IoTEdge\\Core",
            "IoTEdge\\Core\\Diagnostic",
            "IoTEdge\\Core\\Infrastructure",
            "IoTEdge\\Core\\Security",
            "IoTEdge\\Documentation",
            "IoTEdge\\FieldGateway"
        };
        
        public static HashSet<BugQuery> GenerateBugQueries()
        {
            HashSet<BugQuery> output = new HashSet<BugQuery>();
            foreach (string area in areas)
            {
                foreach (BugPriority priority in BugPriorityExtension.BugPriorities)
                {
                    string titleBase = $"{area}-{BugPriorityExtension.DisplayName(priority)}";

                    string inProgressTitle = $"{titleBase}-Started";
                    output.Add(new BugQuery(inProgressTitle, area, priority, true));

                    string notInProgressTitle = $"{titleBase}-Not-Started";
                    output.Add(new BugQuery(notInProgressTitle, area, priority, false));
                }
            }

            return output;
        }
    }
}

