// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class TestInfoReportMetadata : ITestReportMetadata
    {
        public TestInfoReportMetadata()
        {
        }

        public string Source => TestConstants.TestInfo.TestResultSource;

        public TestReportType TestReportType => TestReportType.TestInfoReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.TestInfo;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
