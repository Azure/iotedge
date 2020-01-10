// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethodReport
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Report.DirectMethodReport;
    using TestResultCoordinator.Reports;

    class DirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodReportGenerator));

        readonly string trackingId;

        public DirectMethodReportGenerator(
            string trackingId,
            string expectedSource,
            ITestResultCollection<TestOperationResult> expectedTestResults,
            string actualSource,
            ITestResultCollection<TestOperationResult> actualTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer,
            Option<NetworkStatusTimeline> networkStatusTimeline)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
        }

        internal string ActualSource { get; }

        internal ITestResultCollection<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal ITestResultCollection<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal Option<NetworkStatusTimeline> NetworkStatusTimeline { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(DirectMethodReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            ulong networkOnSuccess = 0;
            ulong networkOffSuccess = 0;
            ulong networkOnToleratedSuccess = 0;
            ulong networkOffToleratedSuccess = 0;
            ulong networkOnFailure = 0;
            ulong networkOffFailure = 0;
            ulong mismatchSuccess = 0;
            ulong mismatchFailure = 0;

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateDataSource(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateDataSource(this.ActualTestResults.Current, this.ActualSource);

                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                        this.NetworkStatusTimeline.Expect<InvalidOperationException>(
                            () => throw new InvalidOperationException("Results are present, but NetworkStatusTimeline is empty."))
                        .GetNetworkControllerStatusAndWithinToleranceAt(this.ExpectedTestResults.Current.CreatedAt);

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    // Found same message in both stores.
                    if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                    {
                        // If network on at time, succeed
                        networkOnSuccess++;
                    }
                    else if (NetworkControllerStatus.Enabled.Equals(networkControllerStatus))
                    {
                        // If network off at time, fail unless in tolerance period
                        if (isWithinTolerancePeriod)
                        {
                            networkOffToleratedSuccess++;
                        }
                        else
                        {
                            networkOffFailure++;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected Result. NetworkControllerStatus was {networkControllerStatus}");
                    }

                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                }
                else // If the expected and actual don't match, we assume actual will be higher sequence # than expected
                {
                    (networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess) =
                        this.CheckUnmatchedResult(this.ExpectedTestResults.Current, networkControllerStatus, isWithinTolerancePeriod, networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess);
                }
            }

            while (hasExpectedResult)
            {
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.Expect<InvalidOperationException>(
                        () => throw new InvalidOperationException("Results are present, but NetworkStatusTimeline is empty."))
                    .GetNetworkControllerStatusAndWithinToleranceAt(this.ExpectedTestResults.Current.CreatedAt);

                (ulong addNetOffSuccess, ulong addNetOnTolSuccess, ulong addNetOnFailure, ulong addMismatchSuccess) =
                        this.CheckUnmatchedResult(this.ExpectedTestResults.Current, networkControllerStatus, isWithinTolerancePeriod, networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess);
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Actual test result source has unexpected results.");

                mismatchFailure++;
                hasActualResult = await this.ActualTestResults.MoveNextAsync();

                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");
            }

            return new DirectMethodReport(
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                networkOnSuccess,
                networkOffSuccess,
                networkOnToleratedSuccess,
                networkOffToleratedSuccess,
                networkOnFailure,
                networkOffFailure,
                mismatchSuccess,
                mismatchFailure);
        }

        (ulong networkOffSuccess, ulong networkOnToleratedSuccess, ulong networkOnFailure, ulong mismatchSuccess) CheckUnmatchedResult(
            TestOperationResult testOperationResult,
            NetworkControllerStatus networkControllerStatus,
            bool isWithinTolerancePeriod,
            ulong networkOffSuccess,
            ulong networkOnToleratedSuccess,
            ulong networkOnFailure,
            ulong mismatchSuccess)
        {
            DirectMethodTestResult dmTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(testOperationResult.Result);
            HttpStatusCode statusCode = JsonConvert.DeserializeObject<HttpStatusCode>(dmTestResult.Result);
            if (HttpStatusCode.InternalServerError.Equals(statusCode))
            {
                if (NetworkControllerStatus.Enabled.Equals(networkControllerStatus))
                {
                    // If the result is a failure AND network is offline, succeed
                    networkOffSuccess++;
                }
                else if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                {
                    if (isWithinTolerancePeriod)
                    {
                        // If result is a failure and network is online, but we're within the tolerance period, succeed
                        networkOnToleratedSuccess++;
                    }
                    else
                    {
                        networkOnFailure++;
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected Result. NetworkControllerStatus was {networkControllerStatus}");
                }
            }
            else
            {
                // Success, but no matching report from Actual store, means mismatch
                mismatchSuccess++;
            }

            return (networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess);
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
