// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    class DeploymentTestReportMetadata : IReportMetadata
    {
        public DeploymentTestReportMetadata(string expectedSource, string actualSource)
        {
            this.ExpectedSource = expectedSource;
            this.ActualSource = actualSource;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public TestReportType TestReportType => TestReportType.DeploymentTestReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.Deployment;

        public override string ToString()
        {
            return $"ExpectedSource: {this.ExpectedSource}, ActualSource: {this.ActualSource}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
