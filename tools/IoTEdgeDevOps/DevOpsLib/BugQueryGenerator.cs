// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BugQueryGenerator
    {
        static readonly BugPriorityGrouping[] priorities = new BugPriorityGrouping[]
        {
            BugPriorityGrouping.Pri1,
            BugPriorityGrouping.Pri2,
            BugPriorityGrouping.PriOther
        };

        static readonly string[] areas = new string[]
        {
            "IoTEdge",
            "IoTEdge\\Agility",
            "IoTEdge\\AppModel",
            "IoTEdge\\AppModel\\K8s",
            "IoTEdge\\Connectivity",
            "IoTEdge\\Core",
            "IoTEdge\\Core\\Diagnostic",
            "IoTEdge\\Core\\Infrastructure",
            "IoTEdge\\Core\\Security",
            "IoTEdge\\Documentation",
            "IoTEdge\\FieldGateway",
            "IoTEdge\\PartnerRequests"
        };

        public static HashSet<BugQuery> GenerateBugQueries()
        {
            HashSet<BugQuery> output = new HashSet<BugQuery>();
            foreach (string area in areas)
            {
                foreach (BugPriorityGrouping priority in priorities)
                {
                    output.Add(new BugQuery(area, priority, true));
                    output.Add(new BugQuery(area, priority, false));
                }
            }

            return output;
        }
    }
}
