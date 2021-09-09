// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    class DirectMethodLongHaulReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public DirectMethodLongHaulReportMetadata(
            string testDescription,
            string senderSource,
            string receiverSource)
            : base(testDescription)
        {
            this.SenderSource = senderSource;
            this.ReceiverSource = receiverSource;
        }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public override TestReportType TestReportType => TestReportType.DirectMethodLongHaulReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.DirectMethod;
    }
}
