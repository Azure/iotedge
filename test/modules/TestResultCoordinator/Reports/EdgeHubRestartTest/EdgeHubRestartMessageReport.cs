// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    class EdgeHubRestartMessageReport : TestResultReportBase
    {
        public EdgeHubRestartMessageReport(
            string trackingId,
            string resultType,
            bool isIncrementalSeqeunce,
            ulong passedMessageCount,
            string senderSource,
            string receiverSource,
            ulong senderMessageCount,
            ulong receiverMessageCount,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram,
            TimeSpan minPeriod,
            TimeSpan maxPeriod,
            TimeSpan medianPeriod,
            TimeSpan meanPeriod,
            double variancePeriodInMilisec)
            : base(trackingId, resultType)
        {
            this.IsIncrementalSeqeunce = isIncrementalSeqeunce;
            this.PassedMessageCount = passedMessageCount;
            this.CompletedStatusHistogram = Preconditions.CheckNotNull(completedStatusHistogram, nameof(completedStatusHistogram));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.SenderMessageCount = senderMessageCount;
            this.ReceiverMessageCount = receiverMessageCount;
            this.MinPeriod = minPeriod;
            this.MaxPeriod = maxPeriod;
            this.MedianPeriod = medianPeriod;
            this.MeanPeriod = meanPeriod;
            this.VariancePeriodInMilisec = variancePeriodInMilisec;
        }

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";

        public override bool IsPassed => this.IsIncrementalSeqeunce && (this.PassedMessageCount == this.SenderMessageCount) && (this.SenderMessageCount > 0);

        public bool IsIncrementalSeqeunce { get; }

        public ulong PassedMessageCount { get; }

        public ulong SenderMessageCount { get; }

        public ulong ReceiverMessageCount { get; }

        public string SenderSource { get; }

        public string ReceiverSource { get; }

        public Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        public TimeSpan MinPeriod { get; }

        public TimeSpan MaxPeriod { get; }

        public TimeSpan MedianPeriod { get; }

        public TimeSpan MeanPeriod { get; }

        public double VariancePeriodInMilisec { get; }
    }
}