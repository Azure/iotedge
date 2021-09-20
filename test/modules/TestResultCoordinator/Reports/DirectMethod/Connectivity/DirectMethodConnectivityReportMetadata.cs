// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.Connectivity
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class DirectMethodConnectivityReportMetadata : TestReportMetadataBase, ITestReportMetadata
    {
        public DirectMethodConnectivityReportMetadata(
            string testDescription,
            Topology topology,
            string senderSource,
            TimeSpan tolerancePeriod,
            string receiverSource = "")
            : base(testDescription)
        {
            this.SenderSource = senderSource;
            this.TolerancePeriod = tolerancePeriod;
            this.ReceiverSource = string.IsNullOrEmpty(receiverSource) ? Option.None<string>() : Option.Some(receiverSource);
            this.Topology = topology;
        }

        public TimeSpan TolerancePeriod { get; }

        public string SenderSource { get; }

        public Option<string> ReceiverSource { get; }

        public Topology Topology;

        public string[] ResultSources =>
            this.ReceiverSource.HasValue ? new string[] { this.SenderSource, this.ReceiverSource.OrDefault() } : new string[] { this.SenderSource };

        public override TestReportType TestReportType => TestReportType.DirectMethodConnectivityReport;

        public override TestOperationResultType TestOperationResultType => TestOperationResultType.DirectMethod;
    }
}
