// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
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

        delegate long parsingSequenceNumber(string seqNumString);

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

                    // BEARWASHERE -- The stats cannot be reported. Make sure these result entries are represent in the warning.
                }

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


            // TODO: In report,
            //    - Calculate min, max, mean, med restartPeriod.
            //    - Use Max(completedRestartPeriod[HttpStatusCode.OK]) to check if it always less the PassableThreshold
            //    - Report numFailedToRestart > 0 but does not count towards failure, give a warning though
            //    - Report messagePreRestart > 1 failure the test citing test code malfunction.
            //    - Check if the time is exceeding the threshold
            //    - Give a warning if the restart cycle does not contain only a message sent.



            

            // BEARWASHERE -- TODO: Deal w/ dups

            // BEARWASHERE -- Define the report format
            return new EdgeHubRestartMessageReport(
                this.TrackingId,
                this.Metadata.TestReportType.ToString(),
                this.Metadata.SenderSource,
                this.Metadata.ReceiverSource);
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