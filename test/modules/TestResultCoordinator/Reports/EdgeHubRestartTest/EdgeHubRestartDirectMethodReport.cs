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
    class EdgeHubRestartDirectMethodReport : TestResultReportBase
    {
        public EdgeHubRestartDirectMethodReport(
            string trackingId,
            string resultType,
            bool isPassing,
            long passedMessageCount,
            Dictionary<string, ulong> messageCount,
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
            this.MessageCount = Preconditions.CheckNotNull(messageCount, nameof(messageCount));
            this.CompletedStatusHistogram = Preconditions.CheckNotNull(completedStatusHistogram, nameof(completedStatusHistogram));
            this.SourceList = new List<string>(messageCount.Keys);
            this.MinPeriod = minPeriod;
            this.MaxPeriod = maxPeriod;
            this.MedianPeriod = medianPeriod;
            this.MeanPeriod = meanPeriod;
            this.VariancePeriod = variancePeriod;
        }

        bool isPassing;

        internal long PassedMessageCount { get; }

        internal Dictionary<string, ulong> MessageCount { get; }

        internal Dictionary<HttpStatusCode, ulong> RestartStatusHistogram { get; }

        internal Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        internal List<string> SourceList { get; }

        internal TimeSpan MinPeriod { get; }

        internal TimeSpan MaxPeriod { get; }

        internal TimeSpan MedianPeriod { get; }

        internal TimeSpan MeanPeriod { get; }

        internal TimeSpan VariancePeriod { get; }

        public override bool IsPassed => this.isPassing;

        public override string Title => $"{this.ResultType} Report between {this.SourceList}";
    }
}