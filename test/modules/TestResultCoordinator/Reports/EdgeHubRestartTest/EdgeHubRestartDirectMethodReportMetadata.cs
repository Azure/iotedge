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
            string receiverSource,
            TimeSpan passableEdgeHubRestartPeriod)
        {
            Preconditions.CheckRange(passableEdgeHubRestartPeriod.Ticks, 0);

            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.PassableEdgeHubRestartPeriod = passableEdgeHubRestartPeriod;
        }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public TimeSpan PassableEdgeHubRestartPeriod { get; }

        public TestReportType TestReportType => TestReportType.EdgeHubRestartDirectMethodReport;

        public TestOperationResultType TestOperationResultType => (TestOperationResultType)Enum.Parse(typeof(TestOperationResultType), this.SenderSource.Split('.').LastOrDefault());
    }
}