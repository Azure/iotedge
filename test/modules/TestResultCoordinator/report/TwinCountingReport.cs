// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System.Collections.ObjectModel;

    class TwinCountingReport<T> : TestResultReportBase
    {
        public TwinCountingReport(string trackingId, string expectedSource, string actualSource, string resultType, ulong totalExpectCount, ulong totalMatchCount, ulong totalPatches, ReadOnlyCollection<string> unmatchedResults)
            : base(trackingId, expectedSource, actualSource, resultType)
        {
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalPatches = totalPatches;
            this.UnmatchedResults = unmatchedResults;
        }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalPatches { get; }

        public ReadOnlyCollection<string> UnmatchedResults { get; }
    }
}
