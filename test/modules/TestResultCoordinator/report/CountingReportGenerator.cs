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
            ITestResultCollection<TestOperationResult> expectedTestResults,
            string actualSource,
            ITestResultCollection<TestOperationResult> actualTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        internal string ActualSource { get; }

        internal ITestResultCollection<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal ITestResultCollection<TestOperationResult> ExpectedTestResults { get; }

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
            Logger.LogInformation($"Start to generate report by {nameof(CountingReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            TestOperationResult lastLoadedResult = default(TestOperationResult);
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateResultCount = 0;
            List<TestOperationResult> unmatchedResults = new List<TestOperationResult>();

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateDataSource(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateDataSource(this.ActualTestResults.Current, this.ActualSource);

                while (hasActualResult && this.TestResultComparer.Matches(lastLoadedResult, this.ActualTestResults.Current))
                {
                    totalDuplicateResultCount++;
                    lastLoadedResult = this.ActualTestResults.Current;
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                }

                totalExpectCount++;

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    lastLoadedResult = this.ActualTestResults.Current;
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    totalMatchCount++;
                }
                else
                {
                    unmatchedResults.Add(this.ExpectedTestResults.Current);
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                }
            }

            while (hasExpectedResult)
            {
                if (this.TestResultComparer.Matches(lastLoadedResult, this.ExpectedTestResults.Current))
                {
                    totalDuplicateResultCount++;
                    lastLoadedResult = this.ExpectedTestResults.Current;
                }
                else
                {
                    unmatchedResults.Add(this.ExpectedTestResults.Current);
                }

                hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                totalExpectCount++;
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                hasActualResult = await this.ActualTestResults.MoveNextAsync();
                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");
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

        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }
        }
    }
}
