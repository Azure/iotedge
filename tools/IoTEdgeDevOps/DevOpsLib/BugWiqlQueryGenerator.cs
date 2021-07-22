// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BugWiqlQueryGenerator
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

        public static HashSet<BugWiqlQuery> GenerateBugQueries()
        {
            HashSet<BugWiqlQuery> output = new HashSet<BugWiqlQuery>();
            foreach (string area in areas)
            {
                foreach (BugPriorityGrouping priority in priorities)
                {
                    output.Add(new BugWiqlQuery(area, priority, true));
                    output.Add(new BugWiqlQuery(area, priority, false));
                }
            }

            return output;
        }
    }
}
