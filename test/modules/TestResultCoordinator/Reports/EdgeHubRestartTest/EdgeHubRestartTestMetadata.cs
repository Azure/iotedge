// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class EdgeHubRestartTestMetadata : ITestReportMetadata
    {
        public EdgeHubRestartTestMetadata(string senderSource, TimeSpan tolerancePeriod, string receiverSource = "")
        {
            this.SenderSource = senderSource;
            this.TolerancePeriod = tolerancePeriod;
            this.ReceiverSource = string.IsNullOrEmpty(receiverSource) ? Option.None<string>() : Option.Some(receiverSource);
        }

        public TimeSpan TolerancePeriod { get; }

        public string SenderSource { get; }

        public Option<string> ReceiverSource { get; }

        public string[] ResultSources =>
            this.ReceiverSource.HasValue ? new string[] { this.SenderSource, this.ReceiverSource.OrDefault() } : new string[] { this.SenderSource };

        public TestReportType TestReportType => TestReportType.EdgeHubRestartReport;

        public TestOperationResultType TestOperationResultType => (TestOperationResultType)Enum.Parse(typeof(TestOperationResultType), SenderSource.Split('.').LastOrDefault()) ;
    }
}
