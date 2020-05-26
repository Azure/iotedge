// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a EdgeHub Restarter report. It is a result of EdgeHub Restarter that include the SDK connection time for the Direct Method receiver.
    /// </summary>
    class EdgeHubRestartDirectMethodReport : TestResultReportBase
    {
        public EdgeHubRestartDirectMethodReport(
            string testDescription,
            string trackingId,
            string resultType,
            ulong passedCount,
            string senderSource,
            string receiverSource,
            ulong senderCount,
            ulong receiverCount,
            TimeSpan medianPeriod)
            : base(testDescription, trackingId, resultType)
        {
            this.PassedCount = passedCount;
            this.SenderCount = senderCount;
            this.ReceiverCount = receiverCount;
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.MedianPeriod = medianPeriod;
        }

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";

        public override bool IsPassed => (this.PassedCount == this.SenderCount) && (this.SenderCount > 0);

        public ulong PassedCount { get; }

        public ulong SenderCount { get; }

        public ulong ReceiverCount { get; }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public TimeSpan MedianPeriod { get; }
    }
}