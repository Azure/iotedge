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

            bool isPassing = true;
            long previousSeqNum = 0;
            ulong passedMessageCount = 0;
            ulong senderMessageCount = 0;
            ulong receiverMessageCount = 0;

            // Value: (source, numOfMessage)
            // Dictionary<string, ulong> messageCount = new Dictionary<string, ulong>()
            // {
            //     { this.SenderSource, 0ul },
            //     { this.ReceiverSource, 0ul }
            // };

            // Value: (completedStatusCode, MessageCompletedTime - EdgeHubRestartedTime)
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram = new Dictionary<HttpStatusCode, List<TimeSpan>>();

            bool hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            bool hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();

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

                // Both sender & receiver have their messages
                senderMessageCount++;
                receiverMessageCount++;

                // Adjust seqeunce number from both source to be equal before doing any comparison
                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                long receiverSeqNum = this.ParseReceiverSequenceNumber(this.ReceiverTestResults.Current.Result);
                long senderSeqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);

                if (receiverSeqNum > senderSeqNum)
                {
                    // Increment sender result to have the same seq as the receiver
                    senderMessageCount = await this.IncrementSenderSequenceNumberAsync(
                        this.SenderTestResults,
                        receiverSeqNum,
                        senderMessageCount,
                        completedStatusHistogram);

                    // Fail the test
                    isPassing = false;
                }

                if (receiverSeqNum < senderSeqNum)
                {
                    // Increment receiver result to have the same seq as the sender
                    receiverMessageCount = await this.IncrementReceiverSequenceNumberAsync(
                        this.ReceiverTestResults,
                        senderSeqNum,
                        receiverMessageCount);

                    // Fail the test
                    isPassing = false;
                }

                // Note: In the current verification, if the receiver obtained more result message than
                //   sender, the test is consider a fail. We are disregarding duplicated/unique seqeunce number
                //   of receiver's result.

                // Check if the current message is passing
                bool isCurrentMessagePassing = true;

                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                string receiverResult = this.ReceiverTestResults.Current.Result;

                // Verified "TrackingId;BatchId;SequenceNumber" altogether.
                isCurrentMessagePassing &= senderResult.GetMessageTestResult() == receiverResult;

                // Verify the sequence number is incremental
                senderSeqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);
                receiverSeqNum = this.ParseReceiverSequenceNumber(receiverResult);
                isCurrentMessagePassing &= senderSeqNum == receiverSeqNum;
                isCurrentMessagePassing &= (previousSeqNum + 1) == senderSeqNum;

                // Verify if the report status is passable
                isCurrentMessagePassing &= senderResult.MessageCompletedStatusCode == HttpStatusCode.OK;

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                // If this message passed, increment the count for a good message
                passedMessageCount += isCurrentMessagePassing ? 1UL : 0UL;
                // Update the overall test status
                isPassing &= isCurrentMessagePassing;

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            // Fail the test if both of the source didn't have the same amount of messages
            isPassing &= !(hasSenderResult ^ hasReceiverResult);

            senderMessageCount = await this.IncrementSenderSequenceNumberAsync(
                this.SenderTestResults,
                long.MaxValue,
                senderMessageCount,
                completedStatusHistogram);

            await this.IncrementReceiverSequenceNumberAsync(
                this.ReceiverTestResults,
                long.MaxValue,
                receiverMessageCount);

            return this.CalculateStatistic(
                isPassing,
                passedMessageCount,
                senderMessageCount,
                receiverMessageCount,
                completedStatusHistogram);
        }

        EdgeHubRestartMessageReport CalculateStatistic(
            bool isPassing,
            ulong passedMessageCount,
            ulong senderMessageCount,
            ulong receiverMessageCount,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            List<TimeSpan> completedPeriods;
            completedStatusHistogram.TryGetValue(HttpStatusCode.OK, out completedPeriods);
            List<TimeSpan> orderedCompletedPeriods = completedPeriods?.OrderBy(p => p.Ticks).ToList();

            TimeSpan minPeriod = TimeSpan.FromTicks(0);
            TimeSpan maxPeriod = TimeSpan.FromTicks(0);
            TimeSpan medianPeriod = TimeSpan.FromTicks(0);
            TimeSpan meanPeriod = TimeSpan.FromTicks(0);
            TimeSpan variancePeriod = TimeSpan.FromTicks(0);
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
                meanPeriod = totalSpan / orderedCompletedPeriods.Count();

                // Compute Variance: var = sum((x - mean)^2) / N
                //                       = sum(x^2) / N - mean^2
                double variancePeriodInMilisec = (totalSpanSquareInMilisec / orderedCompletedPeriods.Count()) - Math.Pow(meanPeriod.TotalMilliseconds, 2);
                variancePeriod = TimeSpan.FromMilliseconds(variancePeriodInMilisec);
            }

            return new EdgeHubRestartMessageReport(
                this.TrackingId,
                this.TestReportType.ToString(),
                isPassing,
                passedMessageCount,
                this.SenderSource,
                this.ReceiverSource,
                senderMessageCount,
                receiverMessageCount,
                completedStatusHistogram,
                minPeriod,
                maxPeriod,
                medianPeriod,
                meanPeriod,
                variancePeriod);
        }

        async Task<ulong> IncrementSenderSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            long targetSequenceNumber,
            ulong messageCount,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            bool isNotEmpty = true;

            EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(resultCollection.Current.Result);
            long seqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                messageCount++;

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                isNotEmpty = await resultCollection.MoveNextAsync();
                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(resultCollection.Current.Result);
                seqNum = this.ParseSenderSequenceNumber(senderResult.SequenceNumber);
            }

            return messageCount;
        }

        async Task<ulong> IncrementReceiverSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            long targetSequenceNumber,
            ulong messageCount)
        {
            bool isNotEmpty = true;
            long seqNum = this.ParseReceiverSequenceNumber(resultCollection.Current.Result);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                messageCount++;

                isNotEmpty = await resultCollection.MoveNextAsync();
                seqNum = this.ParseReceiverSequenceNumber(resultCollection.Current.Result);
            }

            return messageCount;
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
        void AddEntryToCompletedStatusHistogram(
            EdgeHubRestartMessageResult senderResult,
            Dictionary<HttpStatusCode, List<TimeSpan>> histogram)
        {
            HttpStatusCode completedStatus = senderResult.MessageCompletedStatusCode;
            TimeSpan completedPeriod = senderResult.MessageCompletedTime - senderResult.EdgeHubRestartedTime;
            // Try to allocate the list if it is the first time HttpStatusCode shows up
            histogram.TryAdd(completedStatus, new List<TimeSpan>());
            histogram[completedStatus].Add(completedPeriod);
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