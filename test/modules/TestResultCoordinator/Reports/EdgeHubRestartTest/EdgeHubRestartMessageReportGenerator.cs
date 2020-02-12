// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        private Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram;

        delegate Task<(ulong count, bool isNotEmpty, long sequenceNumber)> MoveNextResultAsync(ulong count);

        internal EdgeHubRestartMessageReportGenerator(
            string trackingId,
            string senderSource,
            string receiverSource,
            TestReportType testReportType,
            ITestResultCollection<TestOperationResult> senderTestResults,
            ITestResultCollection<TestOperationResult> receiverTestResults)
        {
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

        internal TestReportType TestReportType { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartMessageReport)} for [{this.SenderSource}] and [{this.ReceiverSource}]");

            bool isIncrementalSeqeunce = true;
            long previousSeqNum = 0;
            ulong passedMessageCount = 0;
            ulong senderMessageCount = 0;
            ulong receiverMessageCount = 0;
            bool hasSenderResult = true;
            long senderSeqNum = 0;
            bool hasReceiverResult = true;
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
                    (senderMessageCount, hasSenderResult, senderSeqNum) = await this.IncrementSequenceNumberAsync(
                        hasSenderResult,
                        this.MoveNextSenderResultAsync,
                        receiverSeqNum,
                        senderMessageCount);
                }

                if (receiverSeqNum < senderSeqNum)
                {
                    // Increment receiver result to have the same seq as the sender
                    (receiverMessageCount, hasReceiverResult, receiverSeqNum) = await this.IncrementSequenceNumberAsync(
                        hasReceiverResult,
                        this.MoveNextReceiverResultAsync,
                        senderSeqNum,
                        receiverMessageCount);
                }

                if (hasSenderResult ^ hasReceiverResult)
                {
                    break;
                }

                // Note: In the current verification, if the receiver obtained more result message than
                //   sender, the test is consider a fail. We are disregarding duplicated/unique seqeunce number
                //   of receiver's result.

                // Check if the current message is passing
                bool isCurrentMessagePassing = true;

                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                string receiverResult = this.ReceiverTestResults.Current.Result;

                // Verified "TrackingId;BatchId;SequenceNumber" altogether.
                isCurrentMessagePassing &= senderResult.GetMessageTestResult() == receiverResult;

                // Verify the sequence number is incremental
                isCurrentMessagePassing &= senderSeqNum == receiverSeqNum;

                // Verify if the report status is passable
                isCurrentMessagePassing &= senderResult.MessageCompletedStatusCode == HttpStatusCode.OK;

                // If this message passed, increment the count for a good message
                passedMessageCount += isCurrentMessagePassing ? 1UL : 0UL;

                // Make sure the sequence number is incremental
                isIncrementalSeqeunce &= (previousSeqNum + 1) == senderSeqNum;
                previousSeqNum++;

                (senderMessageCount, hasSenderResult, senderSeqNum) = await this.MoveNextSenderResultAsync(senderMessageCount);
                (receiverMessageCount, hasReceiverResult, receiverSeqNum) = await this.MoveNextReceiverResultAsync(receiverMessageCount);
            }

            (senderMessageCount, _, _) = await this.IncrementSequenceNumberAsync(
                hasSenderResult,
                this.MoveNextSenderResultAsync,
                long.MaxValue,
                senderMessageCount);

            (receiverMessageCount, _, _) = await this.IncrementSequenceNumberAsync(
                hasReceiverResult,
                this.MoveNextReceiverResultAsync,
                long.MaxValue,
                receiverMessageCount);

            return this.CalculateStatistic(
                isIncrementalSeqeunce,
                passedMessageCount,
                senderMessageCount,
                receiverMessageCount);
        }

        EdgeHubRestartMessageReport CalculateStatistic(
            bool isIncrementalSeqeunce,
            ulong passedMessageCount,
            ulong senderMessageCount,
            ulong receiverMessageCount)
        {
            List<TimeSpan> completedPeriods;
            this.completedStatusHistogram.TryGetValue(HttpStatusCode.OK, out completedPeriods);
            List<TimeSpan> orderedCompletedPeriods = completedPeriods?.OrderBy(p => p.Ticks).ToList();

            TimeSpan minPeriod = TimeSpan.FromTicks(0);
            TimeSpan maxPeriod = TimeSpan.FromTicks(0);
            TimeSpan medianPeriod = TimeSpan.FromTicks(0);
            TimeSpan meanPeriod = TimeSpan.FromTicks(0);
            double variancePeriodInMilisec = 0.0;
            if (orderedCompletedPeriods != null)
            {
                minPeriod = orderedCompletedPeriods.First();
                maxPeriod = orderedCompletedPeriods.Last();

                if ((orderedCompletedPeriods.Count & 0b1) == 0b1)
                {
                    // If odd, pick the middle value
                    medianPeriod = orderedCompletedPeriods[orderedCompletedPeriods.Count >> 1];
                }
                else
                {
                    // If even, average the middle values
                    medianPeriod =
                        (orderedCompletedPeriods[orderedCompletedPeriods.Count >> 1] +
                        orderedCompletedPeriods[(orderedCompletedPeriods.Count >> 1) - 1]) / 2;
                }

                // Compute Mean
                TimeSpan totalSpan = TimeSpan.FromTicks(0);
                double totalSpanSquareInMilisec = 0.0;
                foreach (TimeSpan eachTimeSpan in orderedCompletedPeriods)
                {
                    totalSpan += eachTimeSpan;
                    totalSpanSquareInMilisec += Math.Pow(eachTimeSpan.TotalMilliseconds, 2);
                }

                // Compute Mean : mean = sum(x) / N
                meanPeriod = totalSpan / Math.Max(orderedCompletedPeriods.Count(), 1);

                // Compute Sample Variance: var = sum((x - mean)^2) / (N - 1)
                //                              = sum(x^2) / (N - 1) - mean^2
                variancePeriodInMilisec = (totalSpanSquareInMilisec / Math.Max(orderedCompletedPeriods.Count() - 1, 1)) - Math.Pow(meanPeriod.TotalMilliseconds, 2);
            }

            return new EdgeHubRestartMessageReport(
                this.TrackingId,
                this.TestReportType.ToString(),
                isIncrementalSeqeunce,
                passedMessageCount,
                this.SenderSource,
                this.ReceiverSource,
                senderMessageCount,
                receiverMessageCount,
                this.completedStatusHistogram,
                minPeriod,
                maxPeriod,
                medianPeriod,
                meanPeriod,
                variancePeriodInMilisec);
        }

        async Task<(ulong resultCount, bool isNotEmpty, long sequenceNum)> IncrementSequenceNumberAsync(
            bool isNotEmpty,
            MoveNextResultAsync MoveNextResultAsync,
            long targetSequenceNumber,
            ulong resultCount)
        {
            long seqNum = 0;

            while (isNotEmpty && (seqNum < targetSequenceNumber))
            {
                (resultCount, isNotEmpty, seqNum) = await MoveNextResultAsync(resultCount);
            }

            return (resultCount: resultCount,
                isNotEmpty: isNotEmpty,
                sequenceNum: seqNum);
        }

        private async Task<(ulong resultCount, bool hasValue, long sequenceNumber)> MoveNextSenderResultAsync(ulong senderResultCount)
        {
            bool hasValue = await this.SenderTestResults.MoveNextAsync();
            long seqNum = 0;
            if (hasValue)
            {
                senderResultCount++;

                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                seqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);

                this.AddEntryToCompletedStatusHistogram(senderResult);
            }

            return (resultCount: senderResultCount,
                hasValue: hasValue,
                sequenceNumber: seqNum);
        }

        private async Task<(ulong resultCount, bool hasValue, long sequenceNumber)> MoveNextReceiverResultAsync(ulong receiverResultCount)
        {
            bool hasValue = await this.ReceiverTestResults.MoveNextAsync();
            long seqNum = 0;
            if (hasValue)
            {
                receiverResultCount++;
                seqNum = this.ParseReceiverSequenceNumber(this.ReceiverTestResults.Current.Result);
            }

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

        //////////////////////////////////////////////////////////////// HELPER LAND
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
            long.TryParse(result.Split(';').LastOrDefault(), out seqNum);
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