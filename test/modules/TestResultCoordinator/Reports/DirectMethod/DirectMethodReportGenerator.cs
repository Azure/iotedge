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
            Option<string> receiverSource,
            Option<ITestResultCollection<TestOperationResult>> receiverTestResults,
            string resultType,
            NetworkStatusTimeline networkStatusTimeline)
        {
            if ((receiverSource.HasValue && !receiverTestResults.HasValue) || (!receiverSource.HasValue && receiverTestResults.HasValue))
            {
                throw new ArgumentException("Provide both receiverSource and receiverTestResults or neither.");
            }

            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = receiverSource;
            this.ReceiverTestResults = receiverTestResults;
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
        }

        internal Option<string> ReceiverSource { get; }

        internal Option<ITestResultCollection<TestOperationResult>> ReceiverTestResults { get; }

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
            bool hasReceiverResult = await this.ReceiverTestResults.Match(async x => await x.MoveNextAsync(), () => Task.FromResult(false));

            while (hasSenderResult)
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.SenderTestResults.Current.CreatedAt);
                this.ValidateNetworkControllerStatus(networkControllerStatus);
                DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.SenderTestResults.Current.Result);

                if (hasReceiverResult)
                {
                    AdditionalCountsAndHasResults additionalCountsAndHasResults =
                        await this.ReceiverOnlyLogicAsync(dmSenderTestResult, hasSenderResult, hasReceiverResult, networkControllerStatus, isWithinTolerancePeriod);
                    mismatchSuccess += additionalCountsAndHasResults.MismatchSuccess;
                    mismatchFailure += additionalCountsAndHasResults.MismatchFailure;
                    hasSenderResult = additionalCountsAndHasResults.HasSenderResult;
                    hasReceiverResult = additionalCountsAndHasResults.HasReceiverResult;
                    if (additionalCountsAndHasResults.MismatchFailure > 0 || additionalCountsAndHasResults.MismatchSuccess > 0)
                    {
                        continue;
                    }
                }

                AdditionalCountsAndHasResults additionalCountsAndHasResultsSendery =
                    await this.SenderOnlyLogic(dmSenderTestResult, networkControllerStatus, isWithinTolerancePeriod, this.SenderTestResults);
                networkOnSuccess += additionalCountsAndHasResultsSendery.NetworkOnSuccess;
                networkOffSuccess += additionalCountsAndHasResultsSendery.NetworkOffSuccess;
                networkOnToleratedSuccess += additionalCountsAndHasResultsSendery.NetworkOnToleratedSuccess;
                networkOffToleratedSuccess += additionalCountsAndHasResultsSendery.NetworkOffToleratedSuccess;
                networkOnFailure += additionalCountsAndHasResultsSendery.NetworkOnFailure;
                networkOffFailure += additionalCountsAndHasResultsSendery.NetworkOffFailure;
                hasSenderResult = additionalCountsAndHasResultsSendery.HasSenderResult;
            }

            while (hasReceiverResult)
            {
                AdditionalCountsAndHasResults additionalCountsAndHasResultsForMismatchFailure = await this.MismatchFailureCase();
                mismatchFailure += additionalCountsAndHasResultsForMismatchFailure.MismatchFailure;
                hasReceiverResult = additionalCountsAndHasResultsForMismatchFailure.HasReceiverResult;
            }

            Logger.LogInformation($"Successfully finished creating DirectMethodReport for Sources [{this.SenderSource}] and [{this.ReceiverSource}]");
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

        async Task<AdditionalCountsAndHasResults> ReceiverOnlyLogicAsync(
            DirectMethodTestResult dmSenderTestResult,
            bool hasSenderResult,
            bool hasReceiverResult,
            NetworkControllerStatus networkControllerStatus,
            bool isWithinTolerancePeriod)
        {
            ulong mismatchSuccess = 0;
            string receiverSource = this.ReceiverSource.OrDefault();
            ITestResultCollection<TestOperationResult> receiverTestResults = this.ReceiverTestResults.OrDefault();
            this.ValidateDataSource(receiverTestResults.Current, receiverSource);
            DirectMethodTestResult dmReceiverTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(receiverTestResults.Current.Result);

            if (!string.Equals(dmSenderTestResult.TrackingId, dmReceiverTestResult.TrackingId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Sequence numbers should not match if the testResults didn't match. SenderTestResult: " +
                    $"{dmSenderTestResult.GetFormattedResult()}. ReceiverTestResult: {dmReceiverTestResult.GetFormattedResult()}");
            }

            if (string.Equals(dmSenderTestResult.SequenceNumber, dmReceiverTestResult.SequenceNumber, StringComparison.OrdinalIgnoreCase))
            {
                hasReceiverResult = await receiverTestResults.MoveNextAsync();
            }
            else
            {
                if (int.Parse(dmSenderTestResult.SequenceNumber) > int.Parse(dmReceiverTestResult.SequenceNumber))
                {
                    return await this.MismatchFailureCase();
                }
                else if (int.Parse(dmSenderTestResult.SequenceNumber) < int.Parse(dmReceiverTestResult.SequenceNumber))
                {
                    if (HttpStatusCode.OK.Equals(dmSenderTestResult.Result) &&
                        (NetworkControllerStatus.Disabled.Equals(networkControllerStatus)
                        || (NetworkControllerStatus.Enabled.Equals(networkControllerStatus) && isWithinTolerancePeriod)))
                    {
                        mismatchSuccess++;
                        hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                        return new AdditionalCountsAndHasResults { MismatchSuccess = mismatchSuccess, HasReceiverResult = hasReceiverResult, HasSenderResult = hasSenderResult };
                    }
                }
            }

            return new AdditionalCountsAndHasResults { HasSenderResult = hasSenderResult, HasReceiverResult = hasReceiverResult };
        }

        async Task<AdditionalCountsAndHasResults> MismatchFailureCase()
        {
            ulong mismatchFailure = 0;
            ITestResultCollection<TestOperationResult> receiverTestResults = this.ReceiverTestResults.OrDefault();

            Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

            mismatchFailure++;

            // Log actual queue items
            Logger.LogError($"Unexpected Receiver test result: {receiverTestResults.Current.Source}, " +
                $"{receiverTestResults.Current.Type}, " +
                $"{receiverTestResults.Current.Result} at " +
                $"{receiverTestResults.Current.CreatedAt}");
            bool hasReceiverResult = await receiverTestResults.MoveNextAsync();

            return new AdditionalCountsAndHasResults { MismatchFailure = mismatchFailure, HasReceiverResult = hasReceiverResult };
        }

        async Task<AdditionalCountsAndHasResults> SenderOnlyLogic(
            DirectMethodTestResult dmSenderTestResult,
            NetworkControllerStatus networkControllerStatus,
            bool isWithinTolerancePeriod,
            ITestResultCollection<TestOperationResult> senderTestResults)
        {
            ulong networkOnSuccess = 0;
            ulong networkOffSuccess = 0;
            ulong networkOnToleratedSuccess = 0;
            ulong networkOffToleratedSuccess = 0;
            ulong networkOnFailure = 0;
            ulong networkOffFailure = 0;
            HttpStatusCode statusCode = dmSenderTestResult.Result;
            if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
            {
                if (HttpStatusCode.OK.Equals(statusCode))
                {
                    networkOnSuccess++;
                }
                else
                {
                    if (isWithinTolerancePeriod)
                    {
                        networkOnToleratedSuccess++;
                    }
                    else
                    {
                        networkOnFailure++;
                    }
                }
            }
            else if (NetworkControllerStatus.Enabled.Equals(networkControllerStatus))
            {
                if (HttpStatusCode.InternalServerError.Equals(statusCode))
                {
                    networkOffSuccess++;
                }
                else if (HttpStatusCode.OK.Equals(dmSenderTestResult.Result))
                {
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
                    throw new InvalidDataException($"Unexpected HttpStatusCode of {statusCode}");
                }
            }

            bool hasSenderResult = await senderTestResults.MoveNextAsync();
            return new AdditionalCountsAndHasResults
            {
                NetworkOnSuccess = networkOnSuccess,
                NetworkOffSuccess = networkOffSuccess,
                NetworkOnToleratedSuccess = networkOnToleratedSuccess,
                NetworkOffToleratedSuccess = networkOffToleratedSuccess,
                NetworkOnFailure = networkOnFailure,
                NetworkOffFailure = networkOffFailure,
                HasSenderResult = hasSenderResult
            };
        }

        void ValidateNetworkControllerStatus(NetworkControllerStatus networkControllerStatus)
        {
            if (!NetworkControllerStatus.Enabled.Equals(networkControllerStatus) &&
                !NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
            {
                throw new InvalidOperationException($"Unexpected Result. NetworkControllerStatus was {networkControllerStatus}");
            }
        }

        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected it to be '{expectedSource}'.");
            }
        }

        struct AdditionalCountsAndHasResults
        {
            public ulong NetworkOnSuccess;
            public ulong NetworkOffSuccess;
            public ulong NetworkOnToleratedSuccess;
            public ulong NetworkOffToleratedSuccess;
            public ulong NetworkOnFailure;
            public ulong NetworkOffFailure;
            public ulong MismatchSuccess;
            public ulong MismatchFailure;
            public bool HasReceiverResult;
            public bool HasSenderResult;
        }
    }
}
