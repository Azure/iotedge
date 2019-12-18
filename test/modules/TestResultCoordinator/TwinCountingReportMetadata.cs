// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using TestResultCoordinator.Report;

    class TwinCountingReportMetadata : IReportMetadata
    {
        public TwinCountingReportMetadata(string expectedSource, string actualSource, TestReportType testReportType, TwinTestPropertyType twinTestPropertyType)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
            this.TestReportType = testReportType;
            this.TwinTestPropertyType = twinTestPropertyType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestReportType TestReportType { get; }

        public TwinTestPropertyType TwinTestPropertyType { get; }

        public TestOperationResultType TestOperationResultType => TestOperationResultType.Twin;
    }
}
