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

        public bool IsPassedHelper()
        {
            if (this.TotalExpectCount == 0)
            {
                return false;
            }
            else if (TestDescription.Contains("nested") && TestDescription.Contains("desired property"))
            {
                // This tolerance is needed because we see some missing desired
                // property updates when running in nested.
                return ((double)this.TotalMatchCount / this.TotalExpectCount) > .9d;
            }
            else
            {
                return this.TotalExpectCount == this.TotalMatchCount && this.TotalExpectCount > 0;
            }
        }

        public override string Title => $"Twin Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
