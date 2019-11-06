// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Models
{
    using DevOpsLib.VstsModels;

    public class DashboardViewModel
    {
        public MasterBranch MasterBranch{ get; set; }
    }

    public class MasterBranch
    {
        public VstsBuild ImagesBuild { get; set; }

        public VstsBuild CIBuild { get; set; }

        public VstsBuild EdgeletCIBuild { get; set; }

        public VstsBuild EdgeletPackagesBuild { get; set; }

        public VstsBuild EndToEndTestBuild { get; set; }

        public VstsBuild LibiohsmCIBuild { get; set; }
    }
}
