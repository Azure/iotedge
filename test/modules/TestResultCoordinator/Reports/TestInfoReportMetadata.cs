// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class TestInfoReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public TestInfoReportMetadata(string testDescription)
        : base(testDescription)
        {
        }

        public string Source => TestConstants.TestInfo.TestResultSource;

        public override TestReportType TestReportType => TestReportType.TestInfoReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.TestInfo;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"TestDescription: {this.TestDescription}, Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
