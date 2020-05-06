// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Models
{
    using System.Collections.Generic;
    using DevOpsLib.VstsModels;

    public class DashboardViewModel
    {
        public MasterBranch MasterBranch { get; set; }
    }

    public class MasterBranch
    {
        public IList<VstsBuild> Builds { get; set; }
    }
}
