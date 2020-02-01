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

            bool isResultMatched = true;
            
            // Value: (source, numOfMessage)
            Dictionary<string, ulong> messageCount = new Dictionary<string, ulong>()
            {
                {nameof(this.SenderTestResults), 0ul},
                {nameof(this.ReceiverTestResults), 0ul}
            };

            // Value: (restartStatusCode, numOfTimesTheStatusCodeHappened)
            Dictionary<HttpStatusCode, ulong> restartStatusCount = new Dictionary<HttpStatusCode, ulong>();

            // Value: (completedStatusCode, MessageCompletedTime - EdgeHubRestartedTime)
            Dictionary<HttpStatusCode, List<TimeSpan>> completedRestartPeriod = new Dictionary<HttpStatusCode, List<TimeSpan>>();

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

                // this.SenderTestResults.Current.Result ---Deserialize()--> EdgeHubRestartMessageResult/EdgeHubRestartDirectMethodResult
                // Adjust seqeunce number from both source to be equal before doing any comparison
                long receiverSeqNum = ParseReceiverSequenceNumber(this.ReceiverTestResults.Current.Result);
                long senderSeqNum = ParseSenderSequenceNumber(this.SenderTestResults.Current.Result);

                if (receiverSeqNum > senderSeqNum)
                {
                    await IncrementAdjustSequenceNumberAsync(
                        this.SenderTestResults,
                        nameof(this.SenderTestResults),
                        ParseSenderSequenceNumber,
                        receiverSeqNum,
                        messageCount);
                }
                if (receiverSeqNum < senderSeqNum)
                {
                    await IncrementAdjustSequenceNumberAsync(
                        this.ReceiverTestResults,
                        nameof(this.ReceiverTestResults),
                        ParseReceiverSequenceNumber,
                        senderSeqNum,
                        messageCount);
                }

                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);
                string receiverResult = this.ReceiverTestResults.Current.Result;

                // Verified "TrackingId;BatchId;SequenceNumber" altogether.
                isResultMatched &= (senderResult.GetMessageTestResult() != receiverResult);

                // Extract restart status code
                HttpStatusCode restartStatus = senderResult.EdgeHubRestartStatusCode;
                if (!restartStatusCount.TryAdd(restartStatus, 1))
                {
                    restartStatusCount[restartStatus]++;
                }

                // Extract completedMessageStatus and the time it takes to complete.
                HttpStatusCode completedStatus = senderResult.MessageCompletedStatusCode;
                TimeSpan completedPeriod = senderResult.MessageCompletedTime - senderResult.EdgeHubRestartedTime;
                // Try to allocate the list if it is the first time HttpStatusCode shows up
                completedRestartPeriod.TryAdd(completedStatus, new List<TimeSpan>());
                completedRestartPeriod[completedStatus].Add(completedPeriod);

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            while (hasSenderResult)
            {
                hasSenderResult = await this.SenderTestResults.MoveNextAsync();

                // Log queue items
                Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");
            }



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

        long ParseReceiverSequenceNumber(string result)
        {
            long seqNum;
            long.TryParse(result.Split(';').LastOrDefault(), out seqNum);
            return seqNum;
        }

        long ParseSenderSequenceNumber(string result)
        {
            EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(result);
            long seqNum;
            long.TryParse(senderResult.SequenceNumber, out seqNum);
            return seqNum;
        }

        async Task IncrementAdjustSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            string key,
            parsingSequenceNumber parse,
            long targetSequenceNumber,
            Dictionary<string, ulong> messageCount)
        {
            bool isNotEmpty = true;
            long seqNum = parse(resultCollection.Current.Result);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                messageCount[key]++;

                isNotEmpty = await resultCollection.MoveNextAsync();
                seqNum = parse(resultCollection.Current.Result);
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
    }
}