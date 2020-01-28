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
            string restarterSource,
            TimeSpan passableEdgeHubRestartPeriod,
            string receiverSource)
        {
            Preconditions.CheckRange(passableEdgeHubRestartPeriod.Ticks, 0);

            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));;
            this.RestarterSource = Preconditions.CheckNonWhiteSpace(restarterSource, nameof(restarterSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.PassableEdgeHubRestartPeriod = passableEdgeHubRestartPeriod;
        }

        public string SenderSource { get; }

        public string RestarterSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public TimeSpan PassableEdgeHubRestartPeriod { get; }

        public TestReportType TestReportType => TestReportType.EdgeHubRestartMessageResult;

        public TestOperationResultType TestOperationResultType => (TestOperationResultType)Enum.Parse(typeof(TestOperationResultType), SenderSource.Split('.').LastOrDefault()) ;
    }
}