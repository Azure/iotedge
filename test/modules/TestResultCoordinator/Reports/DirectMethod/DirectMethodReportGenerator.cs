// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
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
    using TestResultCoordinator.Reports;

    sealed class DirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodReportGenerator));

        readonly string trackingId;

        internal DirectMethodReportGenerator(
            string trackingId,
            string expectedSource,
            ITestResultCollection<TestOperationResult> expectedTestResults,
            string actualSource,
            ITestResultCollection<TestOperationResult> actualTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer,
            NetworkStatusTimeline networkStatusTimeline)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
        }

        internal string ActualSource { get; }

        internal ITestResultCollection<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal ITestResultCollection<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal NetworkStatusTimeline NetworkStatusTimeline { get; }

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
                        this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.ExpectedTestResults.Current.CreatedAt);
                if (!NetworkControllerStatus.Enabled.Equals(networkControllerStatus) &&
                    !NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                {
                    throw new InvalidOperationException($"Unexpected Result. NetworkControllerStatus was {networkControllerStatus}");
                }

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

                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                }
                else // If the expected and actual don't match, we assume actual will be higher sequence # than expected
                {
                    UnmatchedResultCounts unmatchedResultCounts =
                        this.CheckUnmatchedResult(this.ExpectedTestResults.Current, networkControllerStatus, isWithinTolerancePeriod);
                    networkOffSuccess += unmatchedResultCounts.NetworkOffSuccess;
                    networkOnToleratedSuccess += unmatchedResultCounts.NetworkOnToleratedSuccess;
                    networkOnFailure += unmatchedResultCounts.NetworkOnFailure;
                    mismatchSuccess += unmatchedResultCounts.MismatchSuccess;
                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                }
            }

            while (hasExpectedResult)
            {
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.ExpectedTestResults.Current.CreatedAt);

                UnmatchedResultCounts unmatchedResultCounts =
                        this.CheckUnmatchedResult(this.ExpectedTestResults.Current, networkControllerStatus, isWithinTolerancePeriod);
                networkOffSuccess += unmatchedResultCounts.NetworkOffSuccess;
                networkOnToleratedSuccess += unmatchedResultCounts.NetworkOnToleratedSuccess;
                networkOnFailure += unmatchedResultCounts.NetworkOnFailure;
                mismatchSuccess += unmatchedResultCounts.MismatchSuccess;
                hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            }

            while (hasActualResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Actual test result source has unexpected results.");

                mismatchFailure++;

                // Log actual queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");

                hasActualResult = await this.ActualTestResults.MoveNextAsync();
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

        UnmatchedResultCounts CheckUnmatchedResult(
            TestOperationResult testOperationResult,
            NetworkControllerStatus networkControllerStatus,
            bool isWithinTolerancePeriod)
        {
            ulong networkOffSuccess = 0;
            ulong networkOnToleratedSuccess = 0;
            ulong networkOnFailure = 0;
            ulong mismatchSuccess = 0;
            // int statusCodeInt = Int32.Parse(testOperationResult.Result.Split(";")[3]);
            // HttpStatusCode statusCode = (HttpStatusCode)statusCodeInt;
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

            return new UnmatchedResultCounts(networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess);
        }

        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }
        }

        struct UnmatchedResultCounts
        {
            public ulong NetworkOffSuccess { get; set; }
            public ulong NetworkOnToleratedSuccess { get; set; }
            public ulong NetworkOnFailure { get; set; }
            public ulong MismatchSuccess { get; set; }

            public UnmatchedResultCounts(ulong networkOffSuccess, ulong networkOnToleratedSuccess, ulong networkOnFailure, ulong mismatchSuccess)
            {
                this.NetworkOffSuccess = networkOffSuccess;
                this.NetworkOnToleratedSuccess = networkOnToleratedSuccess;
                this.NetworkOnFailure = networkOnFailure;
                this.MismatchSuccess = mismatchSuccess;
            }
        }
    }
}
