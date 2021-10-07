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
            "IoT Edge",
            "IoT Edge\\Agility",
            "IoT Edge\\AppModel",
            "IoT Edge\\AppModel\\K8s",
            "IoT Edge\\Connectivity",
            "IoT Edge\\Core",
            "IoT Edge\\Core\\Diagnostic",
            "IoT Edge\\Core\\Infrastructure",
            "IoT Edge\\Core\\Security",
            "IoT Edge\\Documentation",
            "IoT Edge\\FieldGateway",
            "IoT Edge\\PartnerRequests"
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
