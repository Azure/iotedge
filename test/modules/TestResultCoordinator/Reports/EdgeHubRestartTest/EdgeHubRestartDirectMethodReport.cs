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
            bool isPassing,
            ulong passedDirectMethodCount,
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
            this.isPassing = isPassing;
            this.PassedDirectMethodCount = passedDirectMethodCount;
            this.SenderMessageCount = senderMessageCount;
            this.ReceiverMessageCount = receiverMessageCount;
            this.CompletedStatusHistogram = Preconditions.CheckNotNull(completedStatusHistogram, nameof(completedStatusHistogram));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.MinPeriod = minPeriod;
            this.MaxPeriod = maxPeriod;
            this.MedianPeriod = medianPeriod;
            this.MeanPeriod = meanPeriod;
            this.VariancePeriodInMilisec = variancePeriodInMilisec;
        }

        bool isPassing;

        public ulong PassedDirectMethodCount { get; }

        public Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        public string SenderSource { get; }

        public ulong SenderMessageCount { get; }

        public string ReceiverSource { get; }

        public ulong ReceiverMessageCount { get; }

        public TimeSpan MinPeriod { get; }

        public TimeSpan MaxPeriod { get; }

        public TimeSpan MedianPeriod { get; }

        public TimeSpan MeanPeriod { get; }

        public double VariancePeriodInMilisec { get; }

        public override bool IsPassed => this.isPassing;

        public override string Title => $"{this.ResultType} Report between {this.SenderSource} and {this.ReceiverSource}";
    }
}