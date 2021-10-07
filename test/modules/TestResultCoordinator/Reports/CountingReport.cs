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
        const string C2dOverMqttTestDescription = "C2D | mqtt";

        public CountingReport(
            string testDescription,
            string trackingId,
            string expectedSource,
            string actualSource,
            string resultType,
            ulong totalExpectCount,
            ulong totalMatchCount,
            ulong totalDuplicateResultCount,
            IReadOnlyList<TestOperationResult> unmatchedResults)
            : base(testDescription, trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectCount = totalExpectCount;
            this.TotalMatchCount = totalMatchCount;
            this.TotalDuplicateResultCount = totalDuplicateResultCount;
            this.UnmatchedResults = unmatchedResults ?? new List<TestOperationResult>();
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectCount { get; }

        public ulong TotalMatchCount { get; }

        public ulong TotalDuplicateResultCount { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

        public override bool IsPassed => this.IsPassedHelper();

        bool IsPassedHelper()
        {
            if (this.TestDescription == C2dOverMqttTestDescription)
            {
                // This tolerance is needed because sometimes we see a few missing C2D messages.
                // When this product issue is resolved, we can remove this failure tolerance.
                return this.TotalExpectCount > 0 && ((double)this.TotalMatchCount / this.TotalMatchCount) > .95d;
            }
            else
            {
                return this.TotalExpectCount > 0 && this.TotalMatchCount == this.TotalExpectCount;
            }
        }

        public override string Title => $"Counting Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
