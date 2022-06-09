// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    sealed class TwinCountingReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCountingReportGenerator));
        readonly string testDescription;
        readonly Topology topology;
        readonly string trackingId;
        readonly string expectedSource;
        readonly IAsyncEnumerator<TestOperationResult> expectedTestResults;
        readonly string actualSource;
        readonly IAsyncEnumerator<TestOperationResult> actualTestResults;
        readonly string resultType;
        readonly ushort unmatchedResultsMaxSize;
        SimpleTestOperationResultComparer testResultComparer;

        internal TwinCountingReportGenerator(
            string testDescription,
            Topology topology,
            string trackingId,
            string expectedSource,
            IAsyncEnumerator<TestOperationResult> expectedTestResults,
            string actualSource,
            IAsyncEnumerator<TestOperationResult> actualTestResults,
            string testOperationResultType,
            SimpleTestOperationResultComparer testResultComparer,
            ushort unmatchedResultsMaxSize)
        {
            this.testDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.topology = topology;
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.expectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.expectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.actualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.actualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.testResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.resultType = Preconditions.CheckNonWhiteSpace(testOperationResultType, nameof(testOperationResultType));
            this.unmatchedResultsMaxSize = Preconditions.CheckRange<ushort>(unmatchedResultsMaxSize, 1);
        }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(TwinCountingReportGenerator)} for Sources [{this.expectedSource}] and [{this.actualSource}]");

            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalPatches = 0;
            ulong totalDuplicates = 0;
            Queue<string> unmatchedResults = new Queue<string>();

            Dictionary<string, DateTime> propertiesUpdated = new Dictionary<string, DateTime>();
            Dictionary<string, DateTime> propertiesReceived = new Dictionary<string, DateTime>();

            while (await this.expectedTestResults.MoveNextAsync())
            {
                Option<TwinTestResult> twinTestResult = this.GetTwinTestResult(this.expectedTestResults.Current);
                Logger.LogDebug($"Expected test results {twinTestResult}");

                twinTestResult.ForEach(
                    r =>
                    {
                        foreach (var prop in r.Properties)
                        {
                            propertiesUpdated.TryAdd(prop.ToString(), this.expectedTestResults.Current.CreatedAt);
                        }
                    });
            }

            while (await this.actualTestResults.MoveNextAsync())
            {
                totalPatches++;

                Option<TwinTestResult> twinTestResult = this.GetTwinTestResult(this.actualTestResults.Current);
                Logger.LogDebug($"Actual test results {twinTestResult}");

                twinTestResult.ForEach(
                    r =>
                    {
                        foreach (var prop in r.Properties)
                        {
                            bool added = propertiesReceived.TryAdd(prop.ToString(), this.actualTestResults.Current.CreatedAt);
                            if (!added)
                            {
                                Logger.LogDebug($"Duplicate for {this.actualSource} {prop.ToString()}");
                                totalDuplicates++;
                            }
                        }
                    });
            }

            foreach (KeyValuePair<string, DateTime> desiredPropertyUpdate in propertiesUpdated)
            {
                totalExpectCount++;

                if (propertiesReceived.ContainsKey(desiredPropertyUpdate.Key))
                {
                    totalMatchCount++;
                }
                else
                {
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, $"{this.expectedSource} {desiredPropertyUpdate.Key}", this.unmatchedResultsMaxSize);
                }
            }

            foreach (KeyValuePair<string, DateTime> desiredPropertyReceived in propertiesReceived)
            {
                if (!propertiesUpdated.ContainsKey(desiredPropertyReceived.Key))
                {
                    Logger.LogError($"[{nameof(TwinCountingReportGenerator)}] Actual test result source has unexpected results.");
                    TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, $"{this.actualSource} {desiredPropertyReceived.Key}", this.unmatchedResultsMaxSize);
                }
            }

            return new TwinCountingReport(
                this.testDescription,
                this.topology,
                this.trackingId,
                this.expectedSource,
                this.actualSource,
                this.resultType,
                totalExpectCount,
                totalMatchCount,
                totalPatches,
                totalDuplicates,
                new List<string>(unmatchedResults).AsReadOnly());
        }

        Option<TwinTestResult> GetTwinTestResult(TestOperationResult current)
        {
            if (!current.Type.Equals(TestOperationResultType.Twin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Option.None<TwinTestResult>();
            }

            TwinTestResult twinTestResult = JsonConvert.DeserializeObject<TwinTestResult>(current.Result);
            return Option.Some(twinTestResult);
        }
    }
}
