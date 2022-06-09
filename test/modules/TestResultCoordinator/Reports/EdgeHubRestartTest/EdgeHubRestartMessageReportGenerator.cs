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

    sealed class EdgeHubRestartMessageReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(EdgeHubRestartMessageReportGenerator));

        // Value: (completedStatusCode, DirectMethodCompletedTime - EdgeHubRestartedTime)
        Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram;

        delegate Task<(ulong count, bool hasValue, long sequenceNumber)> MoveNextResultAsync(ulong count);

        internal EdgeHubRestartMessageReportGenerator(
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
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartMessageReport)} for [{this.SenderSource}] and [{this.ReceiverSource}]");

            bool isDiscontinuousSequenceNumber = false;
            long previousSeqNum = 0;
            ulong passedMessageCount = 0;
            ulong senderMessageCount = 0;
            ulong receiverMessageCount = 0;
            bool hasSenderResult = false;
            long senderSeqNum = 0;
            bool hasReceiverResult = false;
            long receiverSeqNum = 0;

            (senderMessageCount, hasSenderResult, senderSeqNum) =
                await this.MoveNextSenderResultAsync(senderMessageCount);

            (receiverMessageCount, hasReceiverResult, receiverSeqNum) =
                await this.MoveNextReceiverResultAsync(receiverMessageCount);

            while (hasSenderResult && hasReceiverResult)
            {
                this.ValidateResult(
                    this.SenderTestResults.Current,
                    this.SenderSource,
                    TestOperationResultType.EdgeHubRestartMessage.ToString());

                this.ValidateResult(
                    this.ReceiverTestResults.Current,
                    this.ReceiverSource,
                    TestOperationResultType.Messages.ToString());

                if (receiverSeqNum > senderSeqNum)
                {
                    // Increment sender result to have the same seq as the receiver
                    (senderMessageCount, hasSenderResult, senderSeqNum) = await this.IterateResultToSequenceNumberAsync(
                        hasSenderResult,
                        this.MoveNextSenderResultAsync,
                        receiverSeqNum,
                        senderMessageCount);
                }

                if ((receiverSeqNum < senderSeqNum) && hasSenderResult)
                {
                    // Increment receiver result to have the same seq as the sender
                    (receiverMessageCount, hasReceiverResult, receiverSeqNum) = await this.IterateResultToSequenceNumberAsync(
                        hasReceiverResult,
                        this.MoveNextReceiverResultAsync,
                        senderSeqNum,
                        receiverMessageCount);
                }

                if (!hasSenderResult || !hasReceiverResult)
                {
                    break;
                }

                if (this.VerifyCurrentResult(senderSeqNum, receiverSeqNum))
                {
                    // If this message passed, increment the count for a good message
                    passedMessageCount++;
                }

                // Make sure the sequence number is incremental
                isDiscontinuousSequenceNumber |= (previousSeqNum + 1) != senderSeqNum;
                previousSeqNum = senderSeqNum;

                (senderMessageCount, hasSenderResult, senderSeqNum) = await this.MoveNextSenderResultAsync(senderMessageCount);
                (receiverMessageCount, hasReceiverResult, receiverSeqNum) = await this.MoveNextReceiverResultAsync(receiverMessageCount);
            }

            (senderMessageCount, _, _) = await this.IterateResultToSequenceNumberAsync(
                hasSenderResult,
                this.MoveNextSenderResultAsync,
                long.MaxValue,
                senderMessageCount);

            (receiverMessageCount, _, _) = await this.IterateResultToSequenceNumberAsync(
                hasReceiverResult,
                this.MoveNextReceiverResultAsync,
                long.MaxValue,
                receiverMessageCount);

            EdgeHubRestartStatistics edgeHubRestartStatistics = new EdgeHubRestartStatistics(this.completedStatusHistogram);
            edgeHubRestartStatistics.CalculateStatistic();
            Logger.LogInformation(JsonConvert.SerializeObject(edgeHubRestartStatistics));

            return new EdgeHubRestartMessageReport(
                this.TestDescription,
                this.TrackingId,
                this.TestReportType.ToString(),
                isDiscontinuousSequenceNumber,
                passedMessageCount,
                this.SenderSource,
                this.ReceiverSource,
                senderMessageCount,
                receiverMessageCount,
                edgeHubRestartStatistics.MedianPeriod);
        }

        bool VerifyCurrentResult(
            long senderSeqNum,
            long receiverSeqNum)
        {
            // Check if the current message is passing
            bool isCurrentMessagePassing = true;

            EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
            string receiverResult = this.ReceiverTestResults.Current.Result;

            // Verified "TrackingId;BatchId;SequenceNumber" altogether.
            isCurrentMessagePassing &= string.Compare(senderResult.GetMessageTestResult(), receiverResult) == 0;

            // Verify the sequence numbers
            isCurrentMessagePassing &= senderSeqNum == receiverSeqNum;

            // Verify if the report status is passable
            isCurrentMessagePassing &= senderResult.MessageCompletedStatusCode == HttpStatusCode.OK;

            // Log the data if the reportin result is failing
            if (!isCurrentMessagePassing)
            {
                Logger.LogDebug($"\n MessageResultVerification = {string.Compare(senderResult.GetMessageTestResult(), receiverResult) == 0}\n\t\t|{senderResult.GetMessageTestResult()}|\n\t\t|{receiverResult}|\n SeqeunceNumber = {senderSeqNum} {receiverSeqNum}\n MessageStatusCode = {senderResult.MessageCompletedStatusCode}\n");
            }

            return isCurrentMessagePassing;
        }

        async Task<(ulong resultCount, bool hasValue, long sequenceNum)> IterateResultToSequenceNumberAsync(
            bool hasValue,
            MoveNextResultAsync MoveNextResultAsync,
            long targetSequenceNumber,
            ulong resultCount)
        {
            long seqNum = 0;

            while (hasValue && (seqNum < targetSequenceNumber))
            {
                (resultCount, hasValue, seqNum) = await MoveNextResultAsync(resultCount);
            }

            return (resultCount: resultCount,
                hasValue: hasValue,
                sequenceNum: seqNum);
        }

        // (sequenceNumber) is only valid if and only if (hasValue) is true
        async Task<(ulong resultCount, bool hasValue, long sequenceNumber)> MoveNextSenderResultAsync(ulong senderResultCount)
        {
            bool hasValue = await this.SenderTestResults.MoveNextAsync();
            long seqNum = 0;

            if (!hasValue)
            {
                return (resultCount: senderResultCount,
                    hasValue: hasValue,
                    sequenceNumber: seqNum);
            }

            senderResultCount++;

            EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
            seqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);

            this.AddEntryToCompletedStatusHistogram(senderResult);

            return (resultCount: senderResultCount,
                hasValue: hasValue,
                sequenceNumber: seqNum);
        }

        // (sequenceNumber) is only valid if and only if (hasValue) is true
        async Task<(ulong resultCount, bool hasValue, long sequenceNumber)> MoveNextReceiverResultAsync(ulong receiverResultCount)
        {
            bool hasValue = await this.ReceiverTestResults.MoveNextAsync();
            long seqNum = 0;

            if (!hasValue)
            {
                return (resultCount: receiverResultCount,
                    hasValue: hasValue,
                    sequenceNumber: seqNum);
            }

            receiverResultCount++;
            seqNum = this.ParseReceiverSequenceNumber(this.ReceiverTestResults.Current.Result);

            return (resultCount: receiverResultCount,
                hasValue: hasValue,
                sequenceNumber: seqNum);
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

        void AddEntryToCompletedStatusHistogram(EdgeHubRestartMessageResult senderResult)
        {
            HttpStatusCode completedStatus = senderResult.MessageCompletedStatusCode;
            TimeSpan completedPeriod = senderResult.MessageCompletedTime - senderResult.EdgeHubRestartedTime;
            // Try to allocate the list if it is the first time HttpStatusCode shows up
            this.completedStatusHistogram.TryAdd(completedStatus, new List<TimeSpan>());
            this.completedStatusHistogram[completedStatus].Add(completedPeriod);
        }

        long ParseReceiverSequenceNumber(string result)
        {
            long seqNum;
            // result = "TrackingId;BatchId;SequenceNumber;"
            string[] tokens = result.Split(';');
            long.TryParse(tokens[2], out seqNum);
            return seqNum;
        }

        long ParseSenderSequenceNumber(string seqNumString)
        {
            long seqNum;
            long.TryParse(seqNumString, out seqNum);
            return seqNum;
        }
    }
}