// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.LegacyTwin
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class LegacyTwinReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public LegacyTwinReportMetadata(
            string testDescription,
            string senderSource)
            : base(testDescription)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
        }

        public string SenderSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource };

        public override TestReportType TestReportType => TestReportType.LegacyTwinReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.LegacyTwin;
    }
}
