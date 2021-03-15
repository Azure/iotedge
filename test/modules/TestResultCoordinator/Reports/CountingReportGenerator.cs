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
            ushort unmatchedResultsMaxSize,
            bool eventHubLongHaulMode)
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
            this.EventHubLongHaulMode = eventHubLongHaulMode;
        }

        internal string ActualSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        public bool EventHubLongHaulMode { get; }

        /// <summary>
        /// Compare 2 data stores and counting expect, match, and duplicate results; and return a counting report.
        /// It will remove consecutive duplicate results when loading from actual store.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns>Test Result Report.</returns>
        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(CountingReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            var lastLoadedResult = default(TestOperationResult);
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateResultCount = 0;
            var unmatchedResults = new Queue<TestOperationResult>();
            bool allActualResultsMatch = false;
            Option<EventHubSpecificReportComponents> eventHubSpecificReportComponents = Option.None<EventHubSpecificReportComponents>();
            Option<DateTime> lastLoadedResultCreatedAt = Option.None<DateTime>();

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateResult(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateResult(this.ActualTestResults.Current, this.ActualSource);

                // Skip any duplicate actual value
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
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.unmatchedResultsMaxSize);
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                }
            }

            // Check duplicates at the end of actual results
            while (hasActualResult && this.TestResultComparer.Matches(lastLoadedResult, this.ActualTestResults.Current))
            {
                totalDuplicateResultCount++;
                lastLoadedResult = this.ActualTestResults.Current;
                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            allActualResultsMatch = totalExpectCount == totalMatchCount;

            while (hasExpectedResult)
            {
                totalExpectCount++;
                TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.unmatchedResultsMaxSize);
                hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            }

            if (this.EventHubLongHaulMode)
            {
                bool stillReceivingFromEventHub = false;
                // If we are are using EventHub to receive messages, we see an issue where EventHub can accrue large delays after
                // running for a while. Therefore, if we are using EventHub with this counting report, we do two things.
                // 1. Match only actual results. We still report all expected results, but matching actual results only.
                // 2. We make sure that the last result we got from EventHub (i.e. the lastLoadedResult) is within our defined tolerance period.
                //    'eventHubDelayTolerance' is an arbitrary tolerance period that we have defined, and can be tuned as needed.
                // TODO: There is either something wrong with the EventHub service or something wrong with the way we are using it,
                // Because we should not be accruing such large delays. If we move off EventHub, we should fix this as well.
                TimeSpan eventHubDelayTolerance = Settings.Current.LongHaulSpecificSettings
                        .Expect<ArgumentException>(
                            () => throw new ArgumentException("TRC must be in long haul mode to be generating an EventHubLongHaul CountingReport"))
                        .EventHubDelayTolerance;
                if (lastLoadedResult == null || lastLoadedResult.CreatedAt < DateTime.UtcNow - eventHubDelayTolerance)
                {
                    stillReceivingFromEventHub = false;
                }
                else
                {
                    stillReceivingFromEventHub = true;
                }

                eventHubSpecificReportComponents = Option.Some(new EventHubSpecificReportComponents
                {
                    StillReceivingFromEventHub = stillReceivingFromEventHub,
                    AllActualResultsMatch = allActualResultsMatch
                });
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");

                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            if (lastLoadedResult != null)
            {
                lastLoadedResultCreatedAt = Option.Some(lastLoadedResult.CreatedAt);
            }

            return new CountingReport(
                this.TestDescription,
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateResultCount,
                new List<TestOperationResult>(unmatchedResults).AsReadOnly(),
                eventHubSpecificReportComponents,
                lastLoadedResultCreatedAt);
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
