// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    class CountingReport : TestResultReportBase
    {
        public CountingReport(
            string testDescription,
            string trackingId,
            string expectedSource,
            string actualSource,
            string resultType,
            ulong totalExpectCount,
            ulong totalMatchCount,
            ulong totalDuplicateResultCount,
            IReadOnlyList<TestOperationResult> unmatchedResults,
            Option<bool> stillReceivingFromEventHub,
            Option<DateTime> lastActualResultTimestamp)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<TestOperationResult>();
            this.StillReceivingFromEventHub = stillReceivingFromEventHub;
            this.LastActualResultTimestamp = lastActualResultTimestamp;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

        // StillReceivingFromEventHub is only for counting reports that use EventHub
        // False means we haven't received a message from EventHub since the tolerance period
        // True means we have received a message since the tolerance period
        // Option.None means that this counting report does not use EventHub at all.
        [JsonConverter(typeof(OptionConverter<bool>))]
        public Option<bool> StillReceivingFromEventHub { get; }

        [JsonConverter(typeof(OptionConverter<DateTime>))]
        public Option<DateTime> LastActualResultTimestamp { get; }

        public override bool IsPassed => this.TotalExpectCount == this.TotalMatchCount && this.TotalExpectCount > 0 && this.StillReceivingFromEventHub.GetOrElse(true);

        public override string Title => $"Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
