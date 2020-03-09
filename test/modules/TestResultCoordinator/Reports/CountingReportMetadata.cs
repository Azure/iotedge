// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class CountingReportMetadata : ITestReportMetadata
    {
        public CountingReportMetadata(string testDescription, string expectedSource, string actualSource, TestOperationResultType testOperationResultType, TestReportType testReportType)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TestOperationResultType = testOperationResultType;
            this.TestReportType = testReportType;
        }

        public string TestDescription { get; }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestOperationResultType TestOperationResultType { get; }

        public TestReportType TestReportType { get; }

        public string[] ResultSources => new string[] { this.ExpectedSource, this.ActualSource };

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestDescription: {this.TestDescription}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
