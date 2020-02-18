// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class EdgeHubRestartMessageReportMetadata : ITestReportMetadata
    {
        public EdgeHubRestartMessageReportMetadata(
            string senderSource,
            string receiverSource)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
        }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public TestReportType TestReportType => TestReportType.EdgeHubRestartMessageReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.EdgeHubRestartMessage;
    }
}