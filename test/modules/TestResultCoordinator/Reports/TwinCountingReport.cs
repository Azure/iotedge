// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Util;

    class TwinCountingReport : TestResultReportBase
    {
        public TwinCountingReport(string testDescription, string trackingId, string expectedSource, string actualSource, string resultType, ulong totalExpectCount, ulong totalMatchCount, ulong totalPatches, ulong totalDuplicates, ReadOnlyCollection<string> unmatchedResults)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalPatchesCount = totalPatches;
            this.TotalDuplicateResultCount = totalDuplicates;
            this.UnmatchedResults = unmatchedResults;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalPatchesCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public ReadOnlyCollection<string> UnmatchedResults { get; }

        public override bool IsPassed => this.IsPassedHelper();

        bool IsPassedHelper()
        {
            if (this.TestDescription.Contains("desired property"))
            {
                return this.TotalExpectCount > 0 && ((double)this.TotalMatchCount / this.TotalExpectCount) > .5d;
            }
            else
            {
                return this.TotalExpectCount > 0 && this.TotalExpectCount == this.TotalMatchCount;
            }
        }

        public override string Title => $"Twin Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
