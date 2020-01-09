// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report.DirectMethodReport
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class DirectMethodReportMetadata : IReportMetadata
    {
        public DirectMethodReportMetadata(string expectedSource, string actualSource, TestReportType testReportType)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
            this.TestOperationResultType = TestOperationResultType.DirectMethod;
            this.TestReportType = testReportType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestReportType TestReportType { get; }

        public TestOperationResultType TestOperationResultType { get; }
    }
}
