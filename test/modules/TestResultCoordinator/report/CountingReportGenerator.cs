// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            ITestResults<TestOperationResult> expectedResults,
            string actualSource,
            ITestResults<TestOperationResult> actualResults,
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

        internal ITestResults<TestOperationResult> ActualResults { get; }

        internal string ExpectedSource { get; }

        internal ITestResults<TestOperationResult> ExpectedResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        /// <summary>
        /// Compare 2 data stores and counting expect, match, and duplicate results; and return a counting report.
        /// It will remove consecutive duplicate results when loading from actual store.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns>Test Result Report.</returns>
        public async Task<ITestResultReport> CreateReportAsync()
        {
            TestOperationResult lastLoadedResult = default(TestOperationResult);
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateResultCount = 0;
            List<TestOperationResult> unmatchedResults = new List<TestOperationResult>();

            bool hasExpectedResult = await this.ExpectedResults.MoveNextAsync();
            bool hasActualResult = await this.ActualResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateStoredDataSource(this.ExpectedResults.Current, this.ExpectedSource);
                this.ValidateStoredDataSource(this.ActualResults.Current, this.ActualSource);

                while (hasActualResult && this.TestResultComparer.Matches(lastLoadedResult, this.ActualResults.Current))
                {
                    totalDuplicateResultCount++;
                    lastLoadedResult = this.ActualResults.Current;
                    hasActualResult = await this.ActualResults.MoveNextAsync();
                }

                totalExpectCount++;

                if (this.TestResultComparer.Matches(this.ExpectedResults.Current, this.ActualResults.Current))
                {
                    lastLoadedResult = this.ActualResults.Current;
                    hasActualResult = await this.ActualResults.MoveNextAsync();
                    hasExpectedResult = await this.ExpectedResults.MoveNextAsync();
                    totalMatchCount++;
                }
                else
                {
                    unmatchedResults.Add(this.ExpectedResults.Current);
                    hasExpectedResult = await this.ExpectedResults.MoveNextAsync();
                }
            }

            while (hasExpectedResult)
            {
                if (this.TestResultComparer.Matches(lastLoadedResult, this.ExpectedResults.Current))
                {
                    totalDuplicateResultCount++;
                    lastLoadedResult = this.ExpectedResults.Current;
                }
                else
                {
                    unmatchedResults.Add(this.ExpectedResults.Current);
                }

                hasExpectedResult = await this.ExpectedResults.MoveNextAsync();
                totalExpectCount++;
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                hasActualResult = await this.ActualResults.MoveNextAsync();
                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualResults.Current.Source}, {this.ActualResults.Current.Type}, {this.ActualResults.Current.Result} at {this.ActualResults.Current.CreatedAt}");
            }

            return new CountingReport<TestOperationResult>(
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateResultCount,
                unmatchedResults.AsReadOnly());
        }

        void ValidateStoredDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }
        }
    }
}
