// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class NetworkControllerReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public NetworkControllerReportMetadata(
            string testDescription,
            string source)
            : base(testDescription)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
        }

        public string Source { get; }

        public override TestReportType TestReportType => TestReportType.NetworkControllerReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.Network;

        public string[] ResultSources => new string[] { this.Source };

        public override string ToString()
        {
            return $"{base.ToString()}, Source: {this.Source}, TestOperationResultType: {this.TestOperationResultType.ToString()}";
        }
    }
}
