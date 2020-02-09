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
            bool isPassing,
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
            TimeSpan variancePeriod)
            : base(trackingId, resultType)
        {
            this.isPassing = isPassing;
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
            this.VariancePeriod = variancePeriod;
        }

        bool isPassing;

        public ulong PassedMessageCount { get; }

        public Dictionary<HttpStatusCode, ulong> RestartStatusHistogram { get; }

        public Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        public string SenderSource { get; }

        public ulong SenderMessageCount { get; }

        public string ReceiverSource { get; }

        public ulong ReceiverMessageCount { get; }

        public TimeSpan MinPeriod { get; }

        public TimeSpan MaxPeriod { get; }

        public TimeSpan MedianPeriod { get; }

        public TimeSpan MeanPeriod { get; }

        public TimeSpan VariancePeriod { get; }

        public override bool IsPassed => this.isPassing;

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";
    }
}