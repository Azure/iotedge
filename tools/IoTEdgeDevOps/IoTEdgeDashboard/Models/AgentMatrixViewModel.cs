// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Models
{
    using System.Collections.Generic;
    using DevOpsLib;

    public class AgentMatrixViewModel
    {
        public AgentMatrix AgentTable { get; set; }

        public ImageBuildViewModel ImageBuild { get; set; }

        public List<IoTEdgeAgent> UnmatchedAgents { get; set; }
    }
}
