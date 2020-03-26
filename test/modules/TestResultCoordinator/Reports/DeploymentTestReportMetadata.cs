// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class DeploymentTestReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public DeploymentTestReportMetadata(
            string testDescription,
            string expectedSource,
            string actualSource)
            : base(testDescription)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public override TestReportType TestReportType => TestReportType.DeploymentTestReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.Deployment;

        public string[] ResultSources => new string[] { this.ExpectedSource, this.ActualSource };

        public override string ToString()
        {
            return $"{base.ToString()}, ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}";
        }
    }
}
