// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System.Collections.Generic;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    /// <typeparam name="T">Test result type</typeparam>
    class CountingReport<T> : TestResultReportBase
    {
        public CountingReport(
            string expectSource,
            string actualSource,
            string resultType,
            long totalExpectCount,
            long totalMatchCount,
            long totalDuplicateResultCount,
            IReadOnlyList<T> unmatchedResults)
            : base(expectSource, actualSource, resultType)
        {
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<T>();
        }

        public long TotalExpectCount { get; }

        public long TotalMatchCount { get; }

        public long TotalDuplicateResultCount { get; }

        public IReadOnlyList<T> UnmatchedResults { get; }
    }
}
