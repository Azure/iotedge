// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Models
{
    using System.Collections.Generic;
    using DevOpsLib;

    public class ImageBuildViewModel
    {
        public AgentDemandSet Group { get; set; }

        public List<IoTEdgeAgent> Agents { get; set; }
    }
}
