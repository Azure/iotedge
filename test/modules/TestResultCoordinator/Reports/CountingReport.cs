// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// This is a counting report to show test result counts. It tracks a number
    /// of result counts in order to give full context of test operation.
    ///
    /// This counting report will fail the test for the following reasons:
    ///     1: Duplicate expected results (not congruent with the design of the TRC)
    ///     2: Unmatched results
    ///
    /// It also supports a special mode if the counting report is tracking event hub
    /// results (i.e. upstream telemetry). This is needed because there are large
    /// delays reading messages from eventhub, so we don't want to fail the tests
    /// for messages that just take a long time to come in. Specifically, this will
    /// allow unmatched results if:
    ///     1:  All missing actual result sequence numbers are higher than the last
    ///         received actual result
    ///     2: We are still receiving messages from eventhub
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
            ulong totalDuplicateExpectedResultCount,
            ulong totalDuplicateActualResultCount,
            ulong totalMisorderedActualResultCount,
            IReadOnlyList<TestOperationResult> unmatchedResults,
            IReadOnlyList<TestOperationResult> duplicateExpectedResults,
            IReadOnlyList<TestOperationResult> duplicateActualResults,
            IReadOnlyList<TestOperationResult> misorderedActualResults,
            Option<EventHubSpecificReportComponents> eventHubSpecificReportComponents,
            Option<DateTime> lastActualResultTimestamp)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalUnmatchedCount = Convert.ToUInt64(unmatchedResults.Count);
            this.TotalDuplicateExpectedResultCount = totalDuplicateExpectedResultCount;
            this.TotalDuplicateActualResultCount = totalDuplicateActualResultCount;
            this.TotalMisorderedActualResultCount = totalMisorderedActualResultCount;
            this.UnmatchedResults = unmatchedResults;
            this.DuplicateExpectedResults = duplicateExpectedResults;
            this.DuplicateActualResults = duplicateActualResults;
            this.MisorderedActualResults = misorderedActualResults;
            this.EventHubSpecificReportComponents = eventHubSpecificReportComponents;
            this.LastActualResultTimestamp = lastActualResultTimestamp;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalUnmatchedCount { get; }

        public ulong TotalDuplicateExpectedResultCount { get; }

        public ulong TotalDuplicateActualResultCount { get; }

        public ulong TotalMisorderedActualResultCount { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

        public IReadOnlyList<TestOperationResult> DuplicateExpectedResults { get; }

        public IReadOnlyList<TestOperationResult> DuplicateActualResults { get; }

        public IReadOnlyList<TestOperationResult> MisorderedActualResults { get; }

        // EventHubSpecificReportComponents is a struct only for LongHaul counting reports that use EventHub.
        // We need to deal with counting reports that involve EventHub differently, because
        // EventHub will have a delay that gets longer as long haul runs. Therefore, we want to pass
        // if 1) all actual results have a matching expected result and 2) We are still receiving messages
        // from EventHub.
        [JsonConverter(typeof(OptionConverter<EventHubSpecificReportComponents>), true)]
        public Option<EventHubSpecificReportComponents> EventHubSpecificReportComponents { get; }

        [JsonConverter(typeof(OptionConverter<DateTime>), true)]
        public Option<DateTime> LastActualResultTimestamp { get; }

        public override bool IsPassed => this.IsPassedHelper();

        public bool IsPassedHelper()
        {
            return this.TotalExpectCount > 0 && this.TotalDuplicateExpectedResultCount == 0 && this.EventHubSpecificReportComponents.Match(
                eh =>
                {
                    return eh.AllActualResultsMatch && eh.StillReceivingFromEventHub;
                },
                () =>
                {
                    return this.TotalExpectCount == this.TotalMatchCount;
                });
        }

        public override string Title => $"Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }

    internal struct EventHubSpecificReportComponents
    {
        public bool StillReceivingFromEventHub;
        public bool AllActualResultsMatch;
    }
}
