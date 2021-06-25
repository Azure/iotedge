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
        readonly ushort enumeratedResultsMaxSize;

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
            this.enumeratedResultsMaxSize = Preconditions.CheckRange<ushort>(unmatchedResultsMaxSize, 1);
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

            var lastLoadedExpectedResult = default(TestOperationResult);
            var lastLoadedActualResult = default(TestOperationResult);

            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateExpectedResultCount = 0;
            ulong totalDuplicateActualResultCount = 0;
            ulong totalMisorderedActualResultCount = 0;

            var unmatchedResults = new Queue<TestOperationResult>();
            var duplicateExpectedResults = new Queue<TestOperationResult>();
            var duplicateActualResults = new Queue<TestOperationResult>();
            var misorderedActualResults = new Queue<TestOperationResult>();

            bool allActualResultsMatch = false;
            Option<EventHubSpecificReportComponents> eventHubSpecificReportComponents = Option.None<EventHubSpecificReportComponents>();
            Option<DateTime> lastLoadedResultCreatedAt = Option.None<DateTime>();

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateResult(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateResult(this.ActualTestResults.Current, this.ActualSource);

                // If we see an actual result with an older sequence number
                // then we know that it came in out of order. So we should
                // record it and skip it.
                if (this.IsActualResultSequenceNumberOlder(this.ActualTestResults.Current, this.ExpectedTestResults.Current))
                {
                    totalMisorderedActualResultCount++;
                    TestReportUtil.EnqueueAndEnforceMaxSize(misorderedActualResults, this.ActualTestResults.Current, this.enumeratedResultsMaxSize);

                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    continue;
                }

                if (this.TestResultComparer.Matches(lastLoadedExpectedResult, this.ExpectedTestResults.Current))
                {
                    totalDuplicateExpectedResultCount++;
                    TestReportUtil.EnqueueAndEnforceMaxSize(duplicateExpectedResults, this.ExpectedTestResults.Current, this.enumeratedResultsMaxSize);

                    // If we encounter a duplicate expected result, we have already
                    // accounted for corresponding actual results in prev iteration
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    continue;
                }

                lastLoadedExpectedResult = this.ExpectedTestResults.Current;

                totalExpectCount++;

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    lastLoadedActualResult = this.ActualTestResults.Current;
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    totalMatchCount++;

                    // Skip any duplicate actual value
                    while (hasActualResult && this.TestResultComparer.Matches(lastLoadedActualResult, this.ActualTestResults.Current))
                    {
                        totalDuplicateActualResultCount++;
                        TestReportUtil.EnqueueAndEnforceMaxSize(duplicateActualResults, this.ActualTestResults.Current, this.enumeratedResultsMaxSize);

                        lastLoadedActualResult = this.ActualTestResults.Current;
                        hasActualResult = await this.ActualTestResults.MoveNextAsync();
                        continue;
                    }
                }
                else
                {
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.enumeratedResultsMaxSize);
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                }
            }

            // Check duplicates at the end of actual results
            while (hasActualResult && this.TestResultComparer.Matches(lastLoadedActualResult, this.ActualTestResults.Current))
            {
                totalDuplicateActualResultCount++;
                TestReportUtil.EnqueueAndEnforceMaxSize(duplicateActualResults, this.ActualTestResults.Current, this.enumeratedResultsMaxSize);

                lastLoadedActualResult = this.ActualTestResults.Current;
                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            allActualResultsMatch = totalExpectCount == totalMatchCount;

            while (hasExpectedResult)
            {
                if (this.TestResultComparer.Matches(lastLoadedExpectedResult, this.ExpectedTestResults.Current))
                {
                    totalDuplicateExpectedResultCount++;
                    TestReportUtil.EnqueueAndEnforceMaxSize(duplicateExpectedResults, this.ExpectedTestResults.Current, this.enumeratedResultsMaxSize);
                }
                else
                {
                    totalExpectCount++;
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.enumeratedResultsMaxSize);
                }

                lastLoadedExpectedResult = this.ExpectedTestResults.Current;
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
                if (lastLoadedActualResult == null || lastLoadedActualResult.CreatedAt < DateTime.UtcNow - eventHubDelayTolerance)
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

                if (this.IsActualResultSequenceNumberOlder(this.ActualTestResults.Current, lastLoadedExpectedResult))
                {
                    totalMisorderedActualResultCount++;
                    TestReportUtil.EnqueueAndEnforceMaxSize(misorderedActualResults, this.ActualTestResults.Current, this.enumeratedResultsMaxSize);
                }

                hasActualResult = await this.ActualTestResults.MoveNextAsync();
            }

            if (lastLoadedActualResult != null)
            {
                lastLoadedResultCreatedAt = Option.Some(lastLoadedActualResult.CreatedAt);
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
                totalMisorderedActualResultCount,
                new List<TestOperationResult>(unmatchedResults).AsReadOnly(),
                new List<TestOperationResult>(duplicateExpectedResults).AsReadOnly(),
                new List<TestOperationResult>(duplicateActualResults).AsReadOnly(),
                new List<TestOperationResult>(misorderedActualResults).AsReadOnly(),
                eventHubSpecificReportComponents,
                lastLoadedResultCreatedAt);
        }

        bool IsActualResultSequenceNumberOlder(TestOperationResult actualResult, TestOperationResult expectedResult)
        {
            // TODO: The controller for TestResultCoordinator takes in a custom type
            // not derived from the original types the test modules send. This
            // means we have to rely on string magic like this to get the sequence
            // numbers. In order to clean this up we should allow the controller to
            // ingest the original type the test modules are sending. Then we
            // can cast to MessageTestResult and grab the sequence number attribute.
            int actualSequenceNumber = int.Parse(actualResult.Result.Split(";")[2]);
            int expectedSequenceNumber = int.Parse(expectedResult.Result.Split(";")[2]);

            return actualSequenceNumber < expectedSequenceNumber;
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
