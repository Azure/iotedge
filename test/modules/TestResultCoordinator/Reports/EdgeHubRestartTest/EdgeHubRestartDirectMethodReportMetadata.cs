// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class EdgeHubRestartDirectMethodReportMetadata : ITestReportMetadata
    {
        public EdgeHubRestartDirectMethodReportMetadata(
            string senderSource,
            string receiverSource)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
        }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public TestReportType TestReportType => TestReportType.EdgeHubRestartDirectMethodReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.EdgeHubRestartDirectMethod;
    }
}