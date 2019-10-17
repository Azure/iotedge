// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Models
{
    using System.Collections.Generic;
    using DevOpsLib;

    public class DashboardViewModel
    {
        public AgentMatrix AgentTable { get; set; }

        public ImageBuildViewModel ImageBuild { get; set; }

        public List<IoTEdgeVstsAgent> UnmatchedAgents { get; set; }
    }
}
