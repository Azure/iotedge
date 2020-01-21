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
            ITestResultComparer<TestOperationResult> testResultComparer,
            NetworkStatusTimeline networkStatusTimeline)
        {
            if (receiverSource.HasValue && !receiverTestResults.HasValue)
            {
                throw new ArgumentException("Can't have receiverSource without receiverTestResults.");
            }

            if (!receiverSource.HasValue && receiverTestResults.HasValue)
            {
                throw new ArgumentException("Can't have receiverTestResults without receiverSource.");
            }

            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = receiverSource;
            this.ReceiverTestResults = receiverTestResults;
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
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
                    string receiverSource = this.ReceiverSource.Expect<ArgumentException>(
                        () => throw new ArgumentException("There must be a receiver source if there are receiver test results"));
                    ITestResultCollection<TestOperationResult> receiverTestResults = this.ReceiverTestResults.Expect<ArgumentException>(
                        () => throw new ArgumentException("Previous check for receiver source passed"));
                    this.ValidateDataSource(receiverTestResults.Current, receiverSource);
                    if (!this.TestResultComparer.Matches(receiverTestResults.Current, this.SenderTestResults.Current))
                    {
                        DirectMethodTestResult dmReceiverTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(receiverTestResults.Current.Result);
                        if (int.Parse(dmSenderTestResult.SequenceNumber) > int.Parse(dmReceiverTestResult.SequenceNumber))
                        {
                            // Log message for unexpected case.
                            Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

                            mismatchFailure++;

                            // Log actual queue items
                            Logger.LogError($"Unexpected Receiver test result:" +
                                $" {receiverTestResults.Current.Source}," +
                                $" {receiverTestResults.Current.Type}, " +
                                $"{receiverTestResults.Current.Result} at " +
                                $"{receiverTestResults.Current.CreatedAt}");
                            hasReceiverResult = await receiverTestResults.MoveNextAsync();
                            continue;
                        }
                        else if (int.Parse(dmSenderTestResult.SequenceNumber) < int.Parse(dmReceiverTestResult.SequenceNumber))
                        {
                            if (HttpStatusCode.OK.Equals((HttpStatusCode)int.Parse(dmSenderTestResult.Result)) &&
                                (NetworkControllerStatus.Disabled.Equals(networkControllerStatus)
                                || (NetworkControllerStatus.Enabled.Equals(networkControllerStatus) && isWithinTolerancePeriod)))
                            {
                                mismatchSuccess++;
                                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                                continue;
                            }
                        }
                        else
                        {
                            throw new InvalidDataException($"Sequence numbers should not match if the testResults didn't match. SenderTestResult: " +
                                $"{dmSenderTestResult.GetFormattedResult()}. ReceiverTestResult: {dmReceiverTestResult.GetFormattedResult()}");
                        }
                    }
                    else
                    {
                        hasReceiverResult = await receiverTestResults.MoveNextAsync();
                    }
                }

                HttpStatusCode statusCode = (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), dmSenderTestResult.Result);
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
                    else
                    {
                        throw new InvalidDataException($"Unexpected HttpStatusCode of {statusCode}");
                    }
                }

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            }

            while (hasReceiverResult)
            {
                string receiverSource = this.ReceiverSource.Expect<ArgumentException>(
                    () => throw new ArgumentException("There must be a receiver source if there are receiver test results"));
                ITestResultCollection<TestOperationResult> receiverTestResults =
                    this.ReceiverTestResults.Expect<ArgumentException>(() => throw new ArgumentException("Previous check for receiver source passed"));

                Logger.LogError($"[{nameof(DirectMethodReportGenerator)}] Receiver test result source has unexpected results.");

                mismatchFailure++;

                // Log actual queue items
                Logger.LogError($"Unexpected Receiver test result: {receiverTestResults.Current.Source}, " +
                    $"{receiverTestResults.Current.Type}, " +
                    $"{receiverTestResults.Current.Result} at " +
                    $"{receiverTestResults.Current.CreatedAt}");
                hasReceiverResult = await receiverTestResults.MoveNextAsync();
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
    }
}
