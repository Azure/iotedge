// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    sealed class EdgeHubRestartDirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(EdgeHubRestartDirectMethodReportGenerator));

        // Value: (completedStatusCode, DirectMethodCompletedTime - EdgeHubRestartedTime)
        Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram;

        delegate Task<(ulong count, bool hasValue, ulong sequenceNumber)> MoveNextResultAsync(ulong count);

        internal EdgeHubRestartDirectMethodReportGenerator(
            string testDescription,
            string trackingId,
            string senderSource,
            string receiverSource,
            TestReportType testReportType,
            IAsyncEnumerator<TestOperationResult> senderTestResults,
            IAsyncEnumerator<TestOperationResult> receiverTestResults)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.TestReportType = testReportType;
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverTestResults = Preconditions.CheckNotNull(receiverTestResults, nameof(receiverTestResults));
            this.completedStatusHistogram = new Dictionary<HttpStatusCode, List<TimeSpan>>();
        }

        internal string TrackingId { get; }

        internal string SenderSource { get; }

        internal string ReceiverSource { get; }

        internal string TestDescription { get; }

        internal TestReportType TestReportType { get; }

        internal IAsyncEnumerator<TestOperationResult> SenderTestResults { get; }

        internal IAsyncEnumerator<TestOperationResult> ReceiverTestResults { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartDirectMethodReport)} for [{this.SenderSource}] and [{this.ReceiverSource}]");

            ulong previousSeqNum = 0;
            ulong passedDirectMethodCount = 0;
            ulong senderResultCount = 0;
            ulong receiverResultCount = 0;
            bool hasSenderResult = false;
            ulong senderSeqNum = 0;
            bool hasReceiverResult = false;
            ulong receiverSeqNum = 0;

            (senderResultCount, hasSenderResult, senderSeqNum) =
                await this.MoveNextSenderResultAsync(senderResultCount);

            (receiverResultCount, hasReceiverResult, receiverSeqNum) =
                await this.MoveNextReceiverResultAsync(receiverResultCount);

            while (hasSenderResult && hasReceiverResult)
            {
                this.ValidateResult(
                    this.SenderTestResults.Current,
                    this.SenderSource,
                    TestOperationResultType.EdgeHubRestartDirectMethod.ToString());

                this.ValidateResult(
                    this.ReceiverTestResults.Current,
                    this.ReceiverSource,
                    TestOperationResultType.DirectMethod.ToString());

                if (receiverSeqNum > senderSeqNum)
                {
                    // Increment sender result to have the same seq as the receiver
                    (senderResultCount, hasSenderResult, senderSeqNum) = await this.IterateResultToSequenceNumberAsync(
                        hasSenderResult,
                        this.MoveNextSenderResultAsync,
                        receiverSeqNum,
                        senderResultCount);
                }

                if ((receiverSeqNum < senderSeqNum) && hasSenderResult)
                {
                    // Increment receiver result to have the same seq as the sender
                    (receiverResultCount, hasReceiverResult, receiverSeqNum) = await this.IterateResultToSequenceNumberAsync(
                        hasReceiverResult,
                        this.MoveNextReceiverResultAsync,
                        senderSeqNum,
                        receiverResultCount);
                }

                if (!hasSenderResult || !hasReceiverResult)
                {
                    break;
                }

                if (this.VerifyCurrentResult(senderSeqNum, receiverSeqNum, previousSeqNum))
                {
                    // If the current DM result is passed, increment the count for a good dm
                    passedDirectMethodCount++;
                }

                previousSeqNum = senderSeqNum;

                (senderResultCount, hasSenderResult, senderSeqNum) = await this.MoveNextSenderResultAsync(senderResultCount);
                (receiverResultCount, hasReceiverResult, receiverSeqNum) = await this.MoveNextReceiverResultAsync(receiverResultCount);
            }

            (senderResultCount, _, _) = await this.IterateResultToSequenceNumberAsync(
                hasSenderResult,
                this.MoveNextSenderResultAsync,
                long.MaxValue,
                senderResultCount);

            (receiverResultCount, _, _) = await this.IterateResultToSequenceNumberAsync(
                hasReceiverResult,
                this.MoveNextReceiverResultAsync,
                long.MaxValue,
                receiverResultCount);

            EdgeHubRestartStatistics edgeHubRestartStatistics = new EdgeHubRestartStatistics(this.completedStatusHistogram);
            edgeHubRestartStatistics.CalculateStatistic();
            Logger.LogInformation(JsonConvert.SerializeObject(edgeHubRestartStatistics));

            return new EdgeHubRestartDirectMethodReport(
                this.TestDescription,
                this.TrackingId,
                this.TestReportType.ToString(),
                passedDirectMethodCount,
                this.SenderSource,
                this.ReceiverSource,
                senderResultCount,
                receiverResultCount,
                edgeHubRestartStatistics.MedianPeriod);
        }

        bool VerifyCurrentResult(
            ulong senderSeqNum,
            ulong receiverSeqNum,
            ulong previousSeqNum)
        {
            // Check if the current dm is passing
            bool isCurrentDirectMethodPassing = true;
            EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);

            // Verified the sequence numbers are the same
            isCurrentDirectMethodPassing &= senderSeqNum == receiverSeqNum;

            // Verified the sequence numbers are incremental
            isCurrentDirectMethodPassing &= senderSeqNum > previousSeqNum;

            // Make sure the status code is passing
            isCurrentDirectMethodPassing &= senderResult.DirectMethodCompletedStatusCode == HttpStatusCode.OK;

            // Log the data if the reportin result is failing
            if (!isCurrentDirectMethodPassing)
            {
                Logger.LogDebug($"\n SeqeunceNumber = {senderSeqNum} {receiverSeqNum}\n DirectMethodStatusCode = {senderResult.DirectMethodCompletedStatusCode}\n");
            }

            return isCurrentDirectMethodPassing;
        }

        async Task<(ulong resultCount, bool hasValue, ulong sequenceNum)> IterateResultToSequenceNumberAsync(
            bool hasValue,
            MoveNextResultAsync MoveNextResultAsync,
            ulong targetSequenceNumber,
            ulong resultCount)
        {
            ulong seqNum = 0;

            while (hasValue && (seqNum < targetSequenceNumber))
            {
                (resultCount, hasValue, seqNum) = await MoveNextResultAsync(resultCount);
            }

            return (resultCount: resultCount,
                hasValue: hasValue,
                sequenceNum: seqNum);
        }

        void ValidateResult(
            TestOperationResult result,
            string expectedSource,
            string testOperationResultType)
        {
            if (!result.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{result.Source}' but expected should be '{expectedSource}'.");
            }

            if (!result.Type.Equals(testOperationResultType, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Result type is '{result.Type}' but expected should be '{testOperationResultType}'.");
            }
        }

        void AddEntryToCompletedStatusHistogram(EdgeHubRestartDirectMethodResult senderResult)
        {
            HttpStatusCode completedStatus = senderResult.DirectMethodCompletedStatusCode;
            TimeSpan completedPeriod = senderResult.DirectMethodCompletedTime - senderResult.EdgeHubRestartedTime;
            // Try to allocate the list if it is the first time HttpStatusCode shows up
            this.completedStatusHistogram.TryAdd(completedStatus, new List<TimeSpan>());
            this.completedStatusHistogram[completedStatus].Add(completedPeriod);
        }

        // (sequenceNumber) is only valid if and only if (hasValue) is true
        async Task<(ulong resultCount, bool hasValue, ulong sequenceNumber)> MoveNextSenderResultAsync(ulong senderResultCount)
        {
            bool hasValue = await this.SenderTestResults.MoveNextAsync();
            ulong seqNum = 0;

            if (!hasValue)
            {
                return (resultCount: senderResultCount,
                    hasValue: hasValue,
                    sequenceNumber: seqNum);
            }

            senderResultCount++;

            EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);
            seqNum = senderResult.SequenceNumber;

            this.AddEntryToCompletedStatusHistogram(senderResult);

            return (resultCount: senderResultCount,
                hasValue: hasValue,
                sequenceNumber: seqNum);
        }

        // (sequenceNumber) is only valid if and only if (hasValue) is true
        async Task<(ulong resultCount, bool hasValue, ulong sequenceNumber)> MoveNextReceiverResultAsync(ulong receiverResultCount)
        {
            bool hasValue = await this.ReceiverTestResults.MoveNextAsync();
            ulong seqNum = 0;

            if (!hasValue)
            {
                return (resultCount: receiverResultCount,
                    hasValue: hasValue,
                    sequenceNumber: seqNum);
            }

            receiverResultCount++;

            DirectMethodTestResult receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);
            seqNum = receiverResult.SequenceNumber;

            return (resultCount: receiverResultCount,
                hasValue: hasValue,
                sequenceNumber: seqNum);
        }
    }
}