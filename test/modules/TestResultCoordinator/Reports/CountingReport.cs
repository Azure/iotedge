// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

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
            Option<bool> stillReceivingFromEventHub)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<TestOperationResult>();
            this.StillReceivingFromEventHub = stillReceivingFromEventHub;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

        public Option<bool> StillReceivingFromEventHub { get; }

        public override bool IsPassed => this.TotalExpectCount == this.TotalMatchCount && this.TotalExpectCount > 0 && this.StillReceivingFromEventHub.GetOrElse(true);

        public override string Title => $"Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
