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

            bool isPassed = true;
            
            ulong senderMessages = 0;
            ulong receiverMessage = 0;
            // Value: (source, numOfMessage)
            Dictionary<string, ulong> messageCount = new Dictionary<string, ulong>();

            // Value: (restartStatusCode, numOfTimesTheStatusCodeHappened)
            Dictionary<HttpStatusCode, ulong> restartStatusCount = new Dictionary<HttpStatusCode, ulong>();
            ulong numSuccessRestart = 0;
            // Check restart HttpStatusCode and if not HTTP.OK, increment this number.
            // The result of failed restart will not be added to completedRestartPeriod.
            ulong numFailedToRestart = 0;

            // TODO: pass this dict and numFailedToRestart to the report
            // Value: (completedStatusCode, MessageCompletedTime - EdgeHubRestartedTime)
            // TODO: In report,
            //    - Calculate min, max, mean, med restartPeriod.
            //    - Use Max(completedRestartPeriod[HttpStatusCode.OK]) to check if it always less the PassableThreshold
            //    - Report numFailedToRestart > 0 but does not count towards failure, give a warning though
            //    - Report messagePreRestart > 1 failure the test citing test code malfunction.
            Dictionary<HttpStatusCode, List<TimeSpan>> completedRestartPeriod = new Dictionary<HttpStatusCode, List<TimeSpan>>();




            bool hasExpectedResult = await this.SenderTestResults.MoveNextAsync();
            bool hasActualResult = await this.ReceiverTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateResult(
                    this.SenderTestResults.Current,
                    this.Metadata.SenderSource,
                    TestOperationResultType.EdgeHubRestartMessage.ToString());

                this.ValidateResult(
                    this.ReceiverTestResults.Current,
                    this.Metadata.ReceiverSource,
                    TestOperationResultType.Messages.ToString());

                //this.SenderTestResults.Current.Result ---Deserialize()--> EdgeHubRestartMessageResult/EdgeHubRestartDirectMethodResult
                EdgeHubRestartMessageResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartMessageResult>(this.SenderTestResults.Current.Result);

                // Verified "TrackingId;BatchId;SequenceNumber" altogether.
                isPassed &= (senderResult.GetMessageTestResult() != this.ReceiverTestResults.Current.Result);

                // Check if EH restart status is Http 200
                isPassed &= (senderResult.EdgeHubRestartStatusCode == HttpStatusCode.OK);

                // Check if message status is HTTP200
                isPassed &= (senderResult.MessageCompletedStatusCode == HttpStatusCode.OK);

                // Check if the time is exceeding the threshold
                isPassed &= (this.Metadata.PassableEdgeHubRestartPeriod >= (senderResult.MessageCompletedTime - senderResult.EdgeHubRestartedTime));

                // Check the sender's TRC if
                // - the sequence is in order
                // - if the status code would fit this nicely
                // - 
            }



            // Give a warning if the restart cycle does not contain only a message sent.

            // BEARWASHERE -- TODO: Deal w/ dups

            // BEARWASHERE -- Define the report format
            return new EdgeHubRestartMessageReport(
                this.TrackingId,
                this.Metadata.TestReportType.ToString(),
                this.Metadata.SenderSource,
                this.Metadata.ReceiverSource);
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