// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class ErrorReportMetadata : ITestReportMetadata
    {
        public ErrorReportMetadata()
        {
        }

        public string Source => TestConstants.Error.TestResultSource;

        public TestReportType TestReportType => TestReportType.ErrorReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.Error;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
