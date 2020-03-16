// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class ErrorReportMetadata : ITestReportMetadata
    {
        public ErrorReportMetadata(string testDescription)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
        }

        public string TestDescription { get; }

        public string Source => TestConstants.Error.TestResultSource;

        public TestReportType TestReportType => TestReportType.ErrorReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.Error;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"TestDescription: {this.TestDescription}, Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
