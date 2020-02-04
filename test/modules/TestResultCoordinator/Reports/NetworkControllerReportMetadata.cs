// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class NetworkControllerReportMetadata : ITestReportMetadata
    {
        public NetworkControllerReportMetadata(string source)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
        }

        public string Source { get; }

        public TestReportType TestReportType => TestReportType.NetworkControllerReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.Network;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}, ReportType: {this.TestReportType.ToString()}";
        }
    }
}
