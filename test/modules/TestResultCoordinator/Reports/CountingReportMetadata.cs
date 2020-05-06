// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class CountingReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public CountingReportMetadata(
            string testDescription,
            string expectedSource,
            string actualSource,
            TestOperationResultType testOperationResultType,
            TestReportType testReportType)
            : base(testDescription)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TestOperationResultType = testOperationResultType;
            this.TestReportType = testReportType;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public override TestOperationResultType TestOperationResultType { get; }

        public override TestReportType TestReportType { get; }

        public string[] ResultSources => new string[] { this.ExpectedSource, this.ActualSource };

        public override string ToString()
        {
            return $"{base.ToString()}, ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}";
        }
    }
}
