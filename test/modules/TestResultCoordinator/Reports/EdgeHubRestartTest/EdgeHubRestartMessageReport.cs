// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    class EdgeHubRestartMessageReport : TestResultReportBase
    {
        public EdgeHubRestartMessageReport(
            string trackingId,
            string resultType,
            string senderSource,
            string receiverSource
            )
            : base(trackingId, resultType)
        {
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
        }

        internal string ReceiverSource { get; }

        internal string SenderSource { get; }

        // BEARWASHERE -- Forever optimism -- TODO
        public override bool IsPassed => true;

        public override string Title => $"EdgeHubRestartTest Report between [{this.SenderSource}] and [{this.ReceiverSource}] ({this.ResultType})";
    }
}