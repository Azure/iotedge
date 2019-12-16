// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class CountingReportMetadata : IReportMetadata
    {
        public CountingReportMetadata(string expectedSource, string actualSource, TestOperationResultType testOperationResultType, TestReportType testReportType)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
            this.TestOperationResultType = testOperationResultType;
            this.TestReportType = testReportType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestOperationResultType TestOperationResultType { get; }

        public TestReportType TestReportType { get; }

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
