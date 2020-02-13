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
            string trackingId,
            string resultType,
            ulong passedDirectMethodCount,
            string senderSource,
            string receiverSource,
            ulong senderResultCount,
            ulong receiverResultCount,
            TimeSpan medianPeriod)
            : base(trackingId, resultType)
        {
            this.PassedDirectMethodCount = passedDirectMethodCount;
            this.SenderResultCount = senderResultCount;
            this.ReceiverResultCount = receiverResultCount;
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.MedianPeriod = medianPeriod;
        }

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";

        public override bool IsPassed => (this.PassedDirectMethodCount == this.SenderResultCount) && (this.SenderResultCount > 0);

        public ulong PassedDirectMethodCount { get; }

        public ulong SenderResultCount { get; }

        public ulong ReceiverResultCount { get; }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        public TimeSpan MedianPeriod { get; }
    }
}