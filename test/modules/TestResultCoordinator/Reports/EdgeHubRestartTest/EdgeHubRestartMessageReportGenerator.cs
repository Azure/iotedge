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
            EdgeHubRestartMessageReportMetadata metadata,
            ITestResultCollection<TestOperationResult> senderTestResults,
            ITestResultCollection<TestOperationResult> receiverTestResults,
            TimeSpan passableEdgeHubRestartPeriod)
        {
            Preconditions.CheckRange(passableEdgeHubRestartPeriod.Ticks, 0);

            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.Metadata = Preconditions.CheckNotNull(metadata, nameof(metadata));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverTestResults = Preconditions.CheckNotNull(receiverTestResults, nameof(receiverTestResults));
            this.PassableEdgeHubRestartPeriod = passableEdgeHubRestartPeriod;
        }

        internal string TrackingId { get; }

        internal EdgeHubRestartMessageReportMetadata Metadata { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        internal TimeSpan PassableEdgeHubRestartPeriod { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartMessageReport)} for [{this.Metadata.SenderSource}] and [{this.Metadata.ReceiverSource}]");

            bool isPassing = true;
            
            // Value: (source, numOfMessage)
            Dictionary<string, ulong> messageCount = new Dictionary<string, ulong>()
            {
                {nameof(this.SenderTestResults), 0ul},
                {nameof(this.ReceiverTestResults), 0ul}
            };

            // Value: (restartStatusCode, numOfTimesTheStatusCodeHappened)
            Dictionary<HttpStatusCode, ulong> restartStatusHistogram = new Dictionary<HttpStatusCode, ulong>();

            // Value: (completedStatusCode, MessageCompletedTime - EdgeHubRestartedTime)
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram = new Dictionary<HttpStatusCode, List<TimeSpan>>();

            bool hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            bool hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();

            while (hasSenderResult && hasReceiverResult)
            {
                this.ValidateResult(
                    this.SenderTestResults.Current,
                    this.Metadata.SenderSource,
                    TestOperationResultType.EdgeHubRestartMessage.ToString());

                this.ValidateResult(
                    this.ReceiverTestResults.Current,
                    this.Metadata.ReceiverSource,
                    TestOperationResultType.Messages.ToString());

                // Both sender & receiver have their messages
                messageCount[nameof(this.SenderTestResults)]++;
                messageCount[nameof(this.ReceiverTestResults)]++;

                // Adjust seqeunce number from both source to be equal before doing any comparison
                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                long receiverSeqNum = ParseReceiverSequenceNumber(this.ReceiverTestResults.Current.Result);
                long senderSeqNum = ParseSenderSequenceNumber(senderResult.SequenceNumber);

                if (receiverSeqNum > senderSeqNum)
                {
                    // Increment sender result to have the same seq as the receiver
                    await IncrementSenderSequenceNumberAsync(
                        this.SenderTestResults,
                        nameof(this.SenderTestResults),
                        receiverSeqNum,
                        messageCount,
                        restartStatusHistogram,
                        completedStatusHistogram);

                    // Fail the test
                    isPassing = false;
                }

                if (receiverSeqNum < senderSeqNum)
                {
                    // Increment receiver result to have the same seq as the sender
                    await IncrementReceiverSequenceNumberAsync(
                        this.ReceiverTestResults,
                        nameof(this.ReceiverTestResults),
                        senderSeqNum,
                        messageCount);

                    // Fail the test
                    isPassing = false;
                }

                // Note: In the current verification, if the receiver obtained more result message than 
                //   sender, the test is consider a fail. We are disregarding duplicated/unique seqeunce number 
                //   of receiver's result.

                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                string receiverResult = this.ReceiverTestResults.Current.Result;

                // Verified "TrackingId;BatchId;SequenceNumber" altogether.
                isPassing &= (senderResult.GetMessageTestResult() != receiverResult);

                // Extract restart status code
                this.IncrementRestartStatusHistogram(
                    senderResult.EdgeHubRestartStatusCode,
                    restartStatusHistogram);

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            // Fail the test
            isPassing &= !(hasSenderResult ^ hasReceiverResult);
            
            await IncrementSenderSequenceNumberAsync(
                this.SenderTestResults,
                nameof(this.SenderTestResults),
                long.MaxValue,
                messageCount,
                restartStatusHistogram,
                completedStatusHistogram);
            
            await IncrementReceiverSequenceNumberAsync(
                this.ReceiverTestResults,
                nameof(this.ReceiverTestResults),
                long.MaxValue,
                messageCount);

            return VerifyResults(
                isPassing,
                messageCount,
                restartStatusHistogram,
                completedStatusHistogram);
        }

        EdgeHubRestartMessageReport VerifyResults(
            bool isPassing,
            Dictionary<string, ulong> messageCount,
            Dictionary<HttpStatusCode, ulong> restartStatusHistogram,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            // TODO: In report,
            //    - Calculate min, max, mean, med, r2 restartPeriod.
            //    - Use Max(completedStatusHistogram[HttpStatusCode.OK]) to check if it always less the PassableThreshold
            //    - Report numFailedToRestart > 0 but does not count towards failure, give a warning though
            //    - Report messagePreRestart > 1 failure the test citing test code malfunction.
            //    - Give a warning if the restart cycle does not contain only a message sent (more than 1 message)

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
                        orderedCompletedPeriods[(orderedCompletedPeriods.Count >> 1)-1])/2;
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
                meanPeriod = totalSpan/orderedCompletedPeriods.Count();
                
                // Compute Variance: var = sum((x - mean)^2) / N 
                //                       = sum(x^2)/N - mean^2
                double variancePeriodInMilisec = (totalSpanSquareInMilisec / orderedCompletedPeriods.Count()) - Math.Pow(meanPeriod.TotalMilliseconds, 2);
                variancePeriod = TimeSpan.FromMilliseconds(variancePeriodInMilisec);
            }

            // Make sure the maximum restart period is within a passable threshold
            isPassing &= maxPeriod < this.Metadata.PassableEdgeHubRestartPeriod;


            // BEARWASHERE -- TODO: what happen if the results contain a dictionary? 
            return new EdgeHubRestartMessageReport(
                bool isPassing,
                Dictionary<string, ulong> messageCount,
                Dictionary<HttpStatusCode, ulong> restartStatusHistogram,
                Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram,
                TimeSpan minPeriod,
                TimeSpan maxPeriod,
                TimeSpan medianPeriod,
                TimeSpan meanPeriod,
                TimeSpan variancePeriod);
        }

        async Task IncrementSenderSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            string key,
            long targetSequenceNumber,
            Dictionary<string, ulong> messageCount,
            Dictionary<HttpStatusCode, ulong> restartStatusHistogram,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            bool isNotEmpty = true;

            EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(resultCollection.Current.Result);
            long seqNum = ParseSenderSequenceNumber(senderResult.SequenceNumber);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                messageCount[key]++;

                // Update histrograms
                this.IncrementRestartStatusHistogram(
                    senderResult.EdgeHubRestartStatusCode,
                    restartStatusHistogram);

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                isNotEmpty = await resultCollection.MoveNextAsync();
                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(resultCollection.Current.Result);
                seqNum = ParseSenderSequenceNumber(senderResult.SequenceNumber);
            }
        }

        async Task IncrementReceiverSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            string key,
            long targetSequenceNumber,
            Dictionary<string, ulong> messageCount)
        {
            bool isNotEmpty = true;
            long seqNum = ParseReceiverSequenceNumber(resultCollection.Current.Result);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                messageCount[key]++;

                isNotEmpty = await resultCollection.MoveNextAsync();
                seqNum = ParseReceiverSequenceNumber(resultCollection.Current.Result);
            }
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
        void IncrementRestartStatusHistogram(
            HttpStatusCode key,
            Dictionary<HttpStatusCode, ulong> histogram)
        {
            if (!histogram.TryAdd(key, 1))
            {
                histogram[key]++;
            }
        }

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