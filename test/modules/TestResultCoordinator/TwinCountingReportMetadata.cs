// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using TestResultCoordinator.Report;

    class TwinCountingReportMetadata : CountingReportMetadata
    {
        public TwinCountingReportMetadata(string expectedSource, string actualSource, TestReportType testReportType, TwinTestPropertyType twinTestPropertyType)
            : base(expectedSource, actualSource, TestOperationResultType.Twin, testReportType)
        {
            this.TwinTestPropertyType = twinTestPropertyType;
        }

        public TwinTestPropertyType TwinTestPropertyType { get; }
    }
}
