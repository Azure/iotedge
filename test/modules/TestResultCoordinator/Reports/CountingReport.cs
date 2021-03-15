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
            Option<EventHubSpecificReportComponents> eventHubSpecificReportComponents,
            Option<DateTime> lastActualResultTimestamp)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<TestOperationResult>();
            this.EventHubSpecificReportComponents = eventHubSpecificReportComponents;
            this.LastActualResultTimestamp = lastActualResultTimestamp;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

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
            return this.EventHubSpecificReportComponents.Match(
                eh =>
                {
                    return eh.AllActualResultsMatch && eh.StillReceivingFromEventHub;
                },
                () =>
                {
                    return this.TotalExpectCount == this.TotalMatchCount && this.TotalExpectCount > 0;
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
