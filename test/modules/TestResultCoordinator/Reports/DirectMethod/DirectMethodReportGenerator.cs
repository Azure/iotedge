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

            while (hasSenderResult)
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) =
                    this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.SenderTestResults.Current.CreatedAt);
                this.ValidateNetworkControllerStatus(networkControllerStatus);
                DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.SenderTestResults.Current.Result);
                if (hasReceiverResult)
                {
                    this.ValidateDataSource(this.ReceiverTestResults.Current, this.ReceiverSource);
                    if (!this.TestResultComparer.Matches(this.ReceiverTestResults.Current, this.SenderTestResults.Current))
                    {
                        DirectMethodTestResult dmReceiverTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);
                        if (int.Parse(dmSenderTestResult.SequenceNumber) > int.Parse(dmReceiverTestResult.SequenceNumber))
                        {
                            // Log message for unexpected case.
                            Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

                            mismatchFailure++;

                            // Log actual queue items
                            Logger.LogError($"Unexpected Receiver test result: {this.ReceiverTestResults.Current.Source}, {this.ReceiverTestResults.Current.Type}, {this.ReceiverTestResults.Current.Result} at {this.ReceiverTestResults.Current.CreatedAt}");
                            hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
                            continue;
                        }
                        else if (int.Parse(dmSenderTestResult.SequenceNumber) < int.Parse(dmReceiverTestResult.SequenceNumber))
                        {
                            if (HttpStatusCode.OK.Equals((HttpStatusCode)int.Parse(dmSenderTestResult.Result)) &&
                                (NetworkControllerStatus.Disabled.Equals(networkControllerStatus) || (NetworkControllerStatus.Enabled.Equals(networkControllerStatus) && isWithinTolerancePeriod)))
                            {
                                mismatchSuccess++;
                                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                                continue;
                            }
                        }
                    }
                    else
                    {
                        hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
                    }
                }

                if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                {
                    if (HttpStatusCode.OK.Equals((HttpStatusCode)int.Parse(dmSenderTestResult.Result)))
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
                    if (HttpStatusCode.InternalServerError.Equals((HttpStatusCode)int.Parse(dmSenderTestResult.Result)))
                    {
                        networkOffSuccess++;
                    }
                    else if (HttpStatusCode.OK.Equals((HttpStatusCode)int.Parse(dmSenderTestResult.Result)))
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
                }

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            }

            while (hasReceiverResult)
            {
                Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

                mismatchFailure++;

                // Log actual queue items
                Logger.LogError($"Unexpected Receiver test result: {this.ReceiverTestResults.Current.Source}, {this.ReceiverTestResults.Current.Type}, {this.ReceiverTestResults.Current.Result} at {this.ReceiverTestResults.Current.CreatedAt}");
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
