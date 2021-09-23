// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class TwinCountingReportMetadata : CountingReportMetadata
    {
        public TwinCountingReportMetadata(
            string testDescription,
            Topology topology,
            string expectedSource,
            string actualSource,
            TestReportType testReportType,
            TwinTestPropertyType twinTestPropertyType)
            : base(testDescription, expectedSource, actualSource, TestOperationResultType.Twin, testReportType, false)
        {
            this.TwinTestPropertyType = twinTestPropertyType;
            this.Topology = topology;
        }

        public TwinTestPropertyType TwinTestPropertyType { get; }
        public Topology Topology { get; }
    }
}
