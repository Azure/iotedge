// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class CountingReportMetadata : IReportMetadata
    {
        public CountingReportMetadata(string expectedSource, string actualSource, TestOperationResultType testOperationResultType, ReportType reportType)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
            this.TestOperationResultType = testOperationResultType;
            this.ReportType = reportType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestOperationResultType TestOperationResultType { get; }

        public ReportType ReportType { get; }

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.ReportType.ToString()}";
        }
    }
}
