// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class CountingReportMetadata : ITestReportMetadata
    {
        public CountingReportMetadata(string expectedSource, string actualSource, string testDescription, TestOperationResultType testOperationResultType, TestReportType testReportType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.TestOperationResultType = testOperationResultType;
            this.TestReportType = testReportType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestOperationResultType TestOperationResultType { get; }

        public TestReportType TestReportType { get; }

        public string[] ResultSources => new string[] { this.ExpectedSource, this.ActualSource };

        public string TestDescription { get; }

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestDescription: {this.TestDescription}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
