// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System.Collections.Generic;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    /// <typeparam name="T">Test result type</typeparam>
    class CountingReport<T> : TestResultReportBase
    {
        public CountingReport(
            string trackingId,
            string expectedSource,
            string actualSource,
            string resultType,
            ulong totalExpectCount,
            ulong totalMatchCount,
            ulong totalDuplicateResultCount,
            IReadOnlyList<T> unmatchedResults)
            : base(trackingId, expectedSource, actualSource, resultType)
        {
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<T>();
        }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public IReadOnlyList<T> UnmatchedResults { get; }
    }
}
