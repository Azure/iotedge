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
            string testDescription,
            string trackingId,
            string senderSource,
            ITestResultCollection<TestOperationResult> senderTestResults,
            Option<string> receiverSource,
            Option<ITestResultCollection<TestOperationResult>> receiverTestResults,
            string resultType,
            NetworkStatusTimeline networkStatusTimeline,
            NetworkControllerType networkControllerType)
        {
            if (receiverSource.HasValue ^ receiverTestResults.HasValue)
            {
                throw new ArgumentException("Provide both receiverSource and receiverTestResults or neither.");
            }

            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = receiverSource;
            this.ReceiverTestResults = receiverTestResults;
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
            this.NetworkControllerType = networkControllerType;
        }

        internal Option<string> ReceiverSource { get; }

        internal Option<ITestResultCollection<TestOperationResult>> ReceiverTestResults { get; }

        internal string SenderSource { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal NetworkStatusTimeline NetworkStatusTimeline { get; }

        internal NetworkControllerType NetworkControllerType { get; }

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
            DirectMethodReportGeneratorMetadata reportGeneratorMetadata;

            while (hasSenderResult)
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.SenderTestResults.Current.CreatedAt);
                this.ValidateNetworkControllerStatus(networkControllerStatus);
                DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.SenderTestResults.Current.Result);

                if (hasReceiverResult)
                {
                    reportGeneratorMetadata = await this.ProcessSenderAndReceiverResults(dmSenderTestResult, hasSenderResult, hasReceiverResult, networkControllerStatus, isWithinTolerancePeriod);
                    mismatchSuccess += reportGeneratorMetadata.MismatchSuccess;
                    mismatchFailure += reportGeneratorMetadata.MismatchFailure;
                    hasSenderResult = reportGeneratorMetadata.HasSenderResult;
                    hasReceiverResult = reportGeneratorMetadata.HasReceiverResult;

                    if (reportGeneratorMetadata.MismatchFailure > 0 || reportGeneratorMetadata.MismatchSuccess > 0)
                    {
                        continue;
                    }
                }

                reportGeneratorMetadata = await this.ProcessSenderTestResults(dmSenderTestResult, networkControllerStatus, isWithinTolerancePeriod, this.SenderTestResults);
                networkOnSuccess += reportGeneratorMetadata.NetworkOnSuccess;
                networkOffSuccess += reportGeneratorMetadata.NetworkOffSuccess;
                networkOnToleratedSuccess += reportGeneratorMetadata.NetworkOnToleratedSuccess;
                networkOffToleratedSuccess += reportGeneratorMetadata.NetworkOffToleratedSuccess;
                networkOnFailure += reportGeneratorMetadata.NetworkOnFailure;
                networkOffFailure += reportGeneratorMetadata.NetworkOffFailure;
                hasSenderResult = reportGeneratorMetadata.HasSenderResult;
            }

            while (hasReceiverResult)
            {
                reportGeneratorMetadata = await this.ProcessMismatchFailureCase();
                mismatchFailure += reportGeneratorMetadata.MismatchFailure;
                hasReceiverResult = reportGeneratorMetadata.HasReceiverResult;
            }

            Logger.LogInformation($"Successfully finished creating DirectMethodReport for Sources [{this.SenderSource}] and [{this.ReceiverSource}]");
            return new DirectMethodReport(
                this.TestDescription,
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

        async Task<DirectMethodReportGeneratorMetadata> ProcessSenderAndReceiverResults(
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

            if (dmSenderTestResult.SequenceNumber == dmReceiverTestResult.SequenceNumber)
            {
                hasReceiverResult = await receiverTestResults.MoveNextAsync();
            }
            else
            {
                if (dmSenderTestResult.SequenceNumber > dmReceiverTestResult.SequenceNumber)
                {
                    return await this.ProcessMismatchFailureCase();
                }
                else if (dmSenderTestResult.SequenceNumber < dmReceiverTestResult.SequenceNumber)
                {
                    if (this.IsMismatchSuccess(dmSenderTestResult, networkControllerStatus, isWithinTolerancePeriod))
                    {
                        mismatchSuccess++;
                        hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                        return new DirectMethodReportGeneratorMetadata { MismatchSuccess = mismatchSuccess, HasReceiverResult = hasReceiverResult, HasSenderResult = hasSenderResult };
                    }
                }
            }

            return new DirectMethodReportGeneratorMetadata { HasSenderResult = hasSenderResult, HasReceiverResult = hasReceiverResult };
        }

        bool IsMismatchSuccess(DirectMethodTestResult dmSenderTestResult, NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod)
        {
            if (HttpStatusCode.OK.Equals(dmSenderTestResult.HttpStatusCode))
            {
                if (!NetworkControllerType.Offline.Equals(this.NetworkControllerType))
                {
                    return true;
                }

                if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus) ||
                    (NetworkControllerStatus.Enabled.Equals(networkControllerStatus) && isWithinTolerancePeriod))
                {
                    return true;
                }
            }

            return false;
        }

        async Task<DirectMethodReportGeneratorMetadata> ProcessMismatchFailureCase()
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

            return new DirectMethodReportGeneratorMetadata { MismatchFailure = mismatchFailure, HasReceiverResult = hasReceiverResult };
        }

        async Task<DirectMethodReportGeneratorMetadata> ProcessSenderTestResults(
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
            HttpStatusCode statusCode = dmSenderTestResult.HttpStatusCode;
            if (!NetworkControllerType.Offline.Equals(this.NetworkControllerType))
            {
                if (HttpStatusCode.OK.Equals(statusCode))
                {
                    networkOnSuccess++;
                }
                else
                {
                    networkOnFailure++;
                }
            }
            else if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
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
                if (HttpStatusCode.NotFound.Equals(statusCode))
                {
                    networkOffSuccess++;
                }
                else if (HttpStatusCode.OK.Equals(statusCode))
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
                else if (HttpStatusCode.InternalServerError.Equals(statusCode))
                {
                    networkOffFailure++;
                }
                else
                {
                    throw new InvalidDataException($"Unexpected HttpStatusCode of {statusCode}");
                }
            }

            bool hasSenderResult = await senderTestResults.MoveNextAsync();
            return new DirectMethodReportGeneratorMetadata
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

        struct DirectMethodReportGeneratorMetadata
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
