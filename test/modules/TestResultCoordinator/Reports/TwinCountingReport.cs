// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.ObjectModel;

    class TwinCountingReport : TestResultReportBase
    {
        public TwinCountingReport(string trackingId, string expectedSource, string actualSource, string resultType, ulong totalExpectCount, ulong totalMatchCount, ulong totalPatches, ulong totalDuplicates, ReadOnlyCollection<string> unmatchedResults)
            : base(trackingId, expectedSource, actualSource, resultType)
        {
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalPatchesCount = totalPatches;
            this.TotalDuplicateResultCount = totalDuplicates;
            this.UnmatchedResults = unmatchedResults;
        }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalPatchesCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public ReadOnlyCollection<string> UnmatchedResults { get; }

        public override bool IsPassed => this.TotalExpectCount == this.TotalMatchCount;
    }
}
