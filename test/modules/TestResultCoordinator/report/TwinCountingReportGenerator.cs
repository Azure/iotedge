// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestOperationResult = TestResultCoordinator.TestOperationResult;

    class TwinCountingReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCountingReportGenerator));
        readonly string trackingId;
        readonly string expectedSource;
        readonly ITestResultCollection<TestOperationResult> expectedTestResults;
        readonly string actualSource;
        readonly ITestResultCollection<TestOperationResult> actualTestResults;
        readonly string resultType;
        SimpleTestOperationResultComparer testResultComparer;

        public TwinCountingReportGenerator(string trackingId, string expectedSource, ITestResultCollection<TestOperationResult> expectedTestResults, string actualSource, ITestResultCollection<TestOperationResult> actualTestResults, string testOperationResultType, SimpleTestOperationResultComparer testResultComparer)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.expectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.expectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.actualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.actualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.testResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.resultType = Preconditions.CheckNonWhiteSpace(testOperationResultType, nameof(testOperationResultType));
        }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(TwinCountingReportGenerator)} for Sources [{this.expectedTestResults}] and [{this.actualSource}]");

            ulong totalExpectCount = 0;
            ulong totalMatchCount = 0;
            ulong totalPatches = 0;
            List<string> unmatchedResults = new List<string>();

            Dictionary<string, DateTime> propertiesUpdated = new Dictionary<string, DateTime>();
            Dictionary<string, DateTime> propertiesReceived = new Dictionary<string, DateTime>();

            while (await this.expectedTestResults.MoveNextAsync())
            {
                Option<TwinTestResult> twinTestResult = this.GetTwinTestResult(this.expectedTestResults.Current);

                twinTestResult.ForEach(
                    r =>
                    {
                        foreach (var prop in r.Properties)
                        {
                            propertiesUpdated.Add(prop.ToString(), this.expectedTestResults.Current.CreatedAt);
                        }
                    });
            }

            while (await this.actualTestResults.MoveNextAsync())
            {
                totalPatches++;

                Option<TwinTestResult> twinTestResult = this.GetTwinTestResult(this.actualTestResults.Current);
                twinTestResult.ForEach(
                    r =>
                    {
                        foreach (var prop in r.Properties)
                        {
                            propertiesReceived.Add(prop.ToString(), this.actualTestResults.Current.CreatedAt);
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
                    unmatchedResults.Add($"{this.expectedSource} {desiredPropertyUpdate.Key}");
                }
            }

            foreach (KeyValuePair<string, DateTime> desiredPropertyReceived in propertiesReceived)
            {
                if (!propertiesUpdated.ContainsKey(desiredPropertyReceived.Key))
                {
                    Logger.LogError($"[{nameof(TwinCountingReportGenerator)}] Actual test result source has unexpected results.");
                    unmatchedResults.Add($"{this.actualSource} {desiredPropertyReceived.Key}");
                }
            }

            return new TwinCountingReport<TestOperationResult>(
                this.trackingId,
                this.expectedSource,
                this.actualSource,
                this.resultType,
                totalExpectCount,
                totalMatchCount,
                totalPatches,
                unmatchedResults.AsReadOnly());
        }

        Option<TwinTestResult> GetTwinTestResult(TestOperationResult current)
        {
            if (!current.Type.Equals(TestOperationResultType.Twin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Option.None<TwinTestResult>();
            }

            Logger.LogDebug($"Deserializing for source {current.Source} result: {current.Result} {current.Type}");
            TwinTestResult twinTestResult = JsonConvert.DeserializeObject<TwinTestResult>(current.Result);
            return Option.Some(twinTestResult);
        }
    }
}
