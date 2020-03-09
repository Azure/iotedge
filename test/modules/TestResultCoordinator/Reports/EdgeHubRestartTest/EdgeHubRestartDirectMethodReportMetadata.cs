// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    class EdgeHubRestartDirectMethodReportMetadata : ITestReportMetadata
    {
        public EdgeHubRestartDirectMethodReportMetadata(
            string testDescription,
            string senderSource,
            string receiverSource)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
        }

        public string TestDescription { get; }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public string[] ResultSources => new string[] { this.SenderSource, this.ReceiverSource };

        public TestReportType TestReportType => TestReportType.EdgeHubRestartDirectMethodReport;

        public TestOperationResultType TestOperationResultType => TestOperationResultType.EdgeHubRestartDirectMethod;
    }
}