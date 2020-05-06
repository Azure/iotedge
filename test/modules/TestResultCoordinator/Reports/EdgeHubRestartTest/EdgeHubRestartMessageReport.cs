// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    class EdgeHubRestartMessageReport : TestResultReportBase
    {
        public EdgeHubRestartMessageReport(
            string testDescription,
            string trackingId,
            string resultType,
            bool isDiscontinuousSequenceNumber,
            ulong passedCount,
            string senderSource,
            string receiverSource,
            ulong senderCount,
            ulong receiverCount,
            TimeSpan medianPeriod)
            : base(testDescription, trackingId, resultType)
        {
            this.IsDiscontinuousSequenceNumber = isDiscontinuousSequenceNumber;
            this.PassedCount = passedCount;
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.SenderCount = senderCount;
            this.ReceiverCount = receiverCount;
            this.MedianPeriod = medianPeriod;
        }

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";

        public override bool IsPassed => !this.IsDiscontinuousSequenceNumber && (this.PassedCount == this.SenderCount) && (this.SenderCount > 0);

        public bool IsDiscontinuousSequenceNumber { get; }

        public ulong PassedCount { get; }

        public ulong SenderCount { get; }

        public ulong ReceiverCount { get; }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public TimeSpan MedianPeriod { get; }
    }
}