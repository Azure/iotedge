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
            string senderSource,
            ITestResultCollection<TestOperationResult> senderTestResults,
            string receiverSource,
            ITestResultCollection<TestOperationResult> ReceiverTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer,
            NetworkStatusTimeline networkStatusTimeline)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.ReceiverTestResults = Preconditions.CheckNotNull(ReceiverTestResults, nameof(ReceiverTestResults));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
        }

        internal string ReceiverSource { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        internal string SenderSource { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal NetworkStatusTimeline NetworkStatusTimeline { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(DirectMethodReportGenerator)} for Sources [{this.SenderSource}] and [{this.ReceiverSource}]");

            ulong networkOnSuccess = 0;
            ulong networkOffSuccess = 0;
            ulong networkOnToleratedSuccess = 0;
            ulong networkOffToleratedSuccess = 0;
            ulong networkOnFailure = 0;
            ulong networkOffFailure = 0;
            ulong mismatchSuccess = 0;
            ulong mismatchFailure = 0;

            bool hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            bool hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();

            while (hasSenderResult && hasReceiverResult)
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                this.ValidateDataSource(this.ReceiverTestResults.Current, this.ReceiverSource);

                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                        this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.SenderTestResults.Current.CreatedAt);
                if (!NetworkControllerStatus.Enabled.Equals(networkControllerStatus) &&
                    !NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                {
                    throw new InvalidOperationException($"Unexpected Result. NetworkControllerStatus was {networkControllerStatus}");
                }

                if (this.TestResultComparer.Matches(this.SenderTestResults.Current, this.ReceiverTestResults.Current))
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

                    hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                    hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
                }
                else // If the sender and receiver don't match, we assume receiver will be higher sequence # than sender
                {
                    UnmatchedResultCounts unmatchedResultCounts =
                        this.CheckUnmatchedResult(this.SenderTestResults.Current, networkControllerStatus, isWithinTolerancePeriod);
                    networkOffSuccess += unmatchedResultCounts.NetworkOffSuccess;
                    networkOnToleratedSuccess += unmatchedResultCounts.NetworkOnToleratedSuccess;
                    networkOnFailure += unmatchedResultCounts.NetworkOnFailure;
                    mismatchSuccess += unmatchedResultCounts.MismatchSuccess;
                    hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                }
            }

            while (hasSenderResult)
            {
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.SenderTestResults.Current.CreatedAt);

                UnmatchedResultCounts unmatchedResultCounts =
                        this.CheckUnmatchedResult(this.SenderTestResults.Current, networkControllerStatus, isWithinTolerancePeriod);
                networkOffSuccess += unmatchedResultCounts.NetworkOffSuccess;
                networkOnToleratedSuccess += unmatchedResultCounts.NetworkOnToleratedSuccess;
                networkOnFailure += unmatchedResultCounts.NetworkOnFailure;
                mismatchSuccess += unmatchedResultCounts.MismatchSuccess;
                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            }

            while (hasReceiverResult)
            {
                // Log message for unexpected case.
                Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

                mismatchFailure++;

                // Log Receiver queue items
                Logger.LogError($"Unexpected receiver test result: {this.ReceiverTestResults.Current.Source}, {this.ReceiverTestResults.Current.Type}, {this.ReceiverTestResults.Current.Result} at {this.ReceiverTestResults.Current.CreatedAt}");

                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            return new DirectMethodReport(
                this.trackingId,
                this.SenderSource,
                this.ReceiverSource,
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
                // Success, but no matching report from receiver store, means mismatch
                mismatchSuccess++;
            }

            return new UnmatchedResultCounts(networkOffSuccess, networkOnToleratedSuccess, networkOnFailure, mismatchSuccess);
        }

        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected it to be '{expectedSource}'.");
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
