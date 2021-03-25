// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This is used to create counting report based on 2 different sources/stores; it will use given test result comparer to determine whether it matches or not.
    /// It also filter out consecutive duplicate results when loading results from actual store.  The default batch size is 500; which is used to control total size of test data loaded into memory.
    /// </summary>
    sealed class CountingReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(CountingReportGenerator));

        readonly string trackingId;
        readonly ushort unmatchedResultsMaxSize;

        internal CountingReportGenerator(
            string testDescription,
            string trackingId,
            string expectedSource,
            IAsyncEnumerator<TestOperationResult> expectedTestResults,
            string actualSource,
            IAsyncEnumerator<TestOperationResult> actualTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer,
            ushort unmatchedResultsMaxSize)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.unmatchedResultsMaxSize = Preconditions.CheckRange<ushort>(unmatchedResultsMaxSize, 1);
        }

        internal string ActualSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        /// <summary>
        /// Compare 2 data stores and counting expect, match, and duplicate results; and return a counting report.
        /// It will remove consecutive duplicate results when loading from actual store.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns>Test Result Report.</returns>
        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(CountingReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            var lastLoadedExpectedResult = default(TestOperationResult);
            var lastLoadedActualResult = default(TestOperationResult);
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateExpectedResultCount = 0;
            ulong totalDuplicateActualResultCount = 0;
            var unmatchedResults = new Queue<TestOperationResult>();

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateResult(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateResult(this.ActualTestResults.Current, this.ActualSource);

                if (this.TestResultComparer.Matches(lastLoadedExpectedResult, this.ExpectedTestResults.Current))
                {
                    totalDuplicateExpectedResultCount++;

                    // If we encounter a duplicate expected result, we have already
                    // accounted for corresponding actual results in prev iteration
                    continue;
                }

                lastLoadedExpectedResult = this.ExpectedTestResults.Current;

                // Skip any duplicate actual value
                while (hasActualResult && this.TestResultComparer.Matches(lastLoadedActualResult, this.ActualTestResults.Current))
                {
                    totalDuplicateActualResultCount++;
                    lastLoadedActualResult = this.ActualTestResults.Current;
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                }

                totalExpectCount++;

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    lastLoadedActualResult = this.ActualTestResults.Current;
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    totalMatchCount++;
                }
                else
                {
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.unmatchedResultsMaxSize);
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                }
            }

            // Check duplicates at the end of actual results
            while (hasActualResult && this.TestResultComparer.Matches(lastLoadedActualResult, this.ActualTestResults.Current))
            {
                totalDuplicateActualResultCount++;
                lastLoadedActualResult = this.ActualTestResults.Current;
                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            while (hasExpectedResult)
            {
                if (this.TestResultComparer.Matches(lastLoadedExpectedResult, this.ExpectedTestResults.Current))
                {
                    totalDuplicateExpectedResultCount++;
                }
                else
                {
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.unmatchedResultsMaxSize);
                }

                lastLoadedExpectedResult = this.ExpectedTestResults.Current;
                totalExpectCount++;
                hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");

                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            return new CountingReport(
                this.TestDescription,
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateExpectedResultCount,
                totalDuplicateActualResultCount,
                new List<TestOperationResult>(unmatchedResults).AsReadOnly());
        }

        void ValidateResult(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }

            if (!current.Type.Equals(this.ResultType, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Result type is '{current.Type}' but expected should be '{this.ResultType}'.");
            }
        }
    }
}
