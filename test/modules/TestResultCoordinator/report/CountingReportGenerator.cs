// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestOperationResult = TestResultCoordinator.TestOperationResult;

    /// <summary>
    /// This is used to create counting report based on 2 different sources/stores; it will use given test result comparer to determine whether it matches or not.
    /// It also filter out consecutive duplicate results when loading results from actual store.  The default batch size is 500; which is used to control total size of test data loaded into memory.
    /// </summary>
    sealed class CountingReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(CountingReportGenerator));

        readonly string trackingId;

        public CountingReportGenerator(
            string trackingId,
            string expectedSource,
            IEnumerable<TestOperationResult> expectedResults,
            string actualSource,
            IEnumerable<TestOperationResult> actualResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedResults = Preconditions.CheckNotNull(expectedResults, nameof(expectedResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualResults = Preconditions.CheckNotNull(actualResults, nameof(actualResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        internal string ActualSource { get; }

        internal IEnumerable<TestOperationResult> ActualResults { get; }

        internal string ExpectedSource { get; }

        internal IEnumerable<TestOperationResult> ExpectedResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        /// <summary>
        /// Compare 2 data stores and counting expect, match, and duplicate results; and return a counting report.
        /// It will remove consecutive duplicate results when loading from actual store.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns>Test Result Report.</returns>
        public Task<ITestResultReport> CreateReportAsync()
        {
            TestOperationResult lastLoadedResult = default(TestOperationResult);
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateResultCount = 0;
            List<TestOperationResult> unmatchedResults = new List<TestOperationResult>();

            var expected = this.ExpectedResults.GetEnumerator();
            var actual = this.ActualResults.GetEnumerator();
            bool hasExpectedResults = expected.MoveNext();
            TestOperationResult expectedResult = expected.Current;
            bool hasActualResults = actual.MoveNext();
            TestOperationResult actualResult = actual.Current;

            while (hasExpectedResults && hasActualResults)
            {
                while (this.TestResultComparer.Matches(lastLoadedResult, actualResult))
                {
                    totalDuplicateResultCount++;
                    hasActualResults = actual.MoveNext();
                    lastLoadedResult = actualResult;
                    actualResult = actual.Current;
                }

                totalExpectCount++;

                if (this.TestResultComparer.Matches(expectedResult, actualResult))
                {
                    hasActualResults = actual.MoveNext();
                    hasExpectedResults = expected.MoveNext();
                    lastLoadedResult = actualResult;
                    actualResult = actual.Current;
                    expectedResult = expected.Current;
                    totalMatchCount++;
                }
                else
                {
                    unmatchedResults.Add(expectedResult);
                    hasExpectedResults = expected.MoveNext();
                    expectedResult = expected.Current;
                }
            }

            while (hasExpectedResults)
            {
                if (this.TestResultComparer.Matches(lastLoadedResult, expectedResult))
                {
                    totalDuplicateResultCount++;
                    lastLoadedResult = expectedResult;
                }
                else
                {
                    unmatchedResults.Add(expectedResult);
                }

                hasExpectedResults = expected.MoveNext();
                expectedResult = expected.Current;
                totalExpectCount++;
            }

            while (hasActualResults)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                actualResult = actual.Current;
                hasActualResults = actual.MoveNext();
                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {actualResult.Source}, {actualResult.Type}, {actualResult.Result} at {actualResult.CreatedAt}");
            }

            var report = new CountingReport<TestOperationResult>(
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateResultCount,
                unmatchedResults.AsReadOnly());
            return Task.FromResult((ITestResultReport)report);
        }
    }
}
