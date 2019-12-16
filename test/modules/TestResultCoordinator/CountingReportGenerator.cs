// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
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
        readonly string expectedSource;
        readonly string actualSource;
        readonly ISequentialStore<TestOperationResult> expectedStore;
        readonly ISequentialStore<TestOperationResult> actualStore;
        readonly string resultType;
        readonly ITestResultComparer<TestOperationResult> testResultComparer;
        readonly int batchSize;

        public CountingReportGenerator(
            string trackingId,
            string expectedSource,
            ISequentialStore<TestOperationResult> expectedStore,
            string actualSource,
            ISequentialStore<TestOperationResult> actualStore,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer,
            int batchSize = 500)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.expectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.expectedStore = Preconditions.CheckNotNull(expectedStore, nameof(expectedStore));
            this.actualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.actualStore = Preconditions.CheckNotNull(actualStore, nameof(actualStore));
            this.resultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.testResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.batchSize = batchSize;
        }

        internal string ActualSource => this.actualSource;

        internal ISequentialStore<TestOperationResult> ActualStore => this.actualStore;

        internal string ExpectedSource => this.expectedSource;

        internal ISequentialStore<TestOperationResult> ExpectedStore => this.expectedStore;

        internal string ResultType => this.resultType;

        internal ITestResultComparer<TestOperationResult> TestResultComparer => this.testResultComparer;

        internal int BatchSize => this.batchSize;

        /// <summary>
        /// Compare 2 data stores and counting expect, match, and duplicate results; and return a counting report.
        /// It will remove consecutive duplicate results when loading from actual store.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns></returns>
        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(CountingReportGenerator)} for Sources [{this.expectedSource}] and [{this.actualSource}]");

            long lastLoadedKeyFromExpectStore = -1;
            long lastLoadedKeyFromActualStore = -1;
            TestOperationResult lastLoadedResult = default(TestOperationResult);

            var expectQueue = new Queue<TestOperationResult>();
            var actualQueue = new Queue<TestOperationResult>();
            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalDuplicateResultCount = 0;
            List<TestOperationResult> unmatchedResults = new List<TestOperationResult>();
            ulong duplicatesFound = 0;

            (lastLoadedKeyFromExpectStore, _, _) = await this.LoadBatchIntoQueueAsync(this.expectedSource, this.expectedStore, lastLoadedKeyFromExpectStore, expectQueue);
            (lastLoadedKeyFromActualStore, lastLoadedResult, duplicatesFound) = await this.LoadBatchIntoQueueAsync(this.actualSource, this.actualStore, lastLoadedKeyFromActualStore, actualQueue, lastLoadedResult);
            totalDuplicateResultCount += duplicatesFound;

            while (expectQueue.Count > 0 && actualQueue.Count > 0)
            {
                TestOperationResult expectedResult = expectQueue.Dequeue();
                TestOperationResult actualResult = actualQueue.Peek();

                totalExpectCount++;

                if (this.testResultComparer.Matches(expectedResult, actualResult))
                {
                    actualQueue.Dequeue();
                    totalMatchCount++;
                }
                else
                {
                    unmatchedResults.Add(expectedResult);
                }

                if (expectQueue.Count == 0 || actualQueue.Count == 0)
                {
                    (lastLoadedKeyFromExpectStore, _, _) = await this.LoadBatchIntoQueueAsync(this.expectedSource, this.expectedStore, lastLoadedKeyFromExpectStore, expectQueue);
                    (lastLoadedKeyFromActualStore, lastLoadedResult, duplicatesFound) = await this.LoadBatchIntoQueueAsync(this.actualSource, this.actualStore, lastLoadedKeyFromActualStore, actualQueue, lastLoadedResult);
                    totalDuplicateResultCount += duplicatesFound;
                }
            }

            while (expectQueue.Count > 0)
            {
                unmatchedResults.Add(expectQueue.Dequeue());
                totalExpectCount++;

                if (expectQueue.Count == 0)
                {
                    (lastLoadedKeyFromExpectStore, _, _) = await this.LoadBatchIntoQueueAsync(this.expectedSource, this.expectedStore, lastLoadedKeyFromExpectStore, expectQueue);
                }
            }

            if (actualQueue.Count > 0)
            {
                //Log message this is unexpected.
                Logger.LogError($"[{nameof(CountingReportGenerator)}] Actual test result source has unexpected results.");

                while (actualQueue.Count > 0)
                {
                    TestOperationResult actualResult = actualQueue.Dequeue();
                    // Log actual queue items
                    Logger.LogError($"Unexpected actual test result: {actualResult.Source}, {actualResult.Type}, {actualResult.Result} at {actualResult.CreatedAt}");

                    if (actualQueue.Count == 0)
                    {
                        (lastLoadedKeyFromActualStore, lastLoadedResult, duplicatesFound) = await this.LoadBatchIntoQueueAsync(this.actualSource, this.actualStore, lastLoadedKeyFromActualStore, actualQueue, lastLoadedResult);
                        totalDuplicateResultCount += duplicatesFound;
                    }
                }
            }

            return new CountingReport<TestOperationResult>(
                this.trackingId,
                this.expectedSource,
                this.actualSource,
                this.resultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateResultCount,
                unmatchedResults.AsReadOnly()
                );
        }

        async Task<(long, TestOperationResult, ulong)> LoadBatchIntoQueueAsync(
            string source,
            ISequentialStore<TestOperationResult> store,
            long lastLoadedPosition,
            Queue<TestOperationResult> resultQueue,
            TestOperationResult lastLoadedResult = null)
        {
            IEnumerable<(long, TestOperationResult)> batch = await store.GetBatch(lastLoadedPosition + 1, this.batchSize);
            long lastLoadedKey = lastLoadedPosition;
            ulong duplicatesFound = 0;

            while (batch.Any())
            {
                foreach ((long, TestOperationResult) values in batch)
                {
                    if (!values.Item2.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Result source is '{values.Item2.Source}' but expected should be '{source}'.");
                    }

                    if (!values.Item2.Type.Equals(this.resultType, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Result type is '{values.Item2.Type}' but expected should be '{this.resultType}'.");
                    }

                    if (lastLoadedResult != null && this.testResultComparer.Matches(lastLoadedResult, values.Item2))
                    {
                        // Skip for duplicate result
                        duplicatesFound++;
                        continue;
                    }

                    resultQueue.Enqueue(values.Item2);
                    lastLoadedKey = values.Item1;
                    lastLoadedResult = values.Item2;
                }

                // load more test results if all are duplicated.
                if (resultQueue.Count > 0)
                {
                    break;
                }

                batch = await store.GetBatch(lastLoadedKey + 1, this.batchSize);
            }

            return (lastLoadedKey, lastLoadedResult, duplicatesFound);
        }
    }
}
