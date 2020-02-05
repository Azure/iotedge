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

    sealed class EdgeHubRestartDirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(EdgeHubRestartDirectMethodReportGenerator));

        internal EdgeHubRestartDirectMethodReportGenerator(
            string trackingId,
            EdgeHubRestartDirectMethodReportMetadata metadata,
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

        internal EdgeHubRestartDirectMethodReportMetadata Metadata { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        internal TimeSpan PassableEdgeHubRestartPeriod { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartDirectMethodReport)} for [{this.Metadata.SenderSource}] and [{this.Metadata.ReceiverSource}]");

            bool isPassing = true;
            long previousSeqNum = 0;
            long passedDirectMethodCount = 0;

            // Value: (source, numOfResults)
            Dictionary<string, ulong> resultCount = new Dictionary<string, ulong>()
            {
                { nameof(this.SenderTestResults), 0ul },
                { nameof(this.ReceiverTestResults), 0ul }
            };

            // Value: (completedStatusCode, DirectMethodCompletedTime - EdgeHubRestartedTime)
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram = new Dictionary<HttpStatusCode, List<TimeSpan>>();

            bool hasSenderResult = await this.SenderTestResults.MoveNextAsync();
            bool hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();

            while (hasSenderResult && hasReceiverResult)
            {
                this.ValidateResult(
                    this.SenderTestResults.Current,
                    this.Metadata.SenderSource,
                    TestOperationResultType.EdgeHubRestartDirectMethod.ToString());

                this.ValidateResult(
                    this.ReceiverTestResults.Current,
                    this.Metadata.ReceiverSource,
                    TestOperationResultType.DirectMethod.ToString());

                // Both sender & receiver have their dm results
                resultCount[nameof(this.SenderTestResults)]++;
                resultCount[nameof(this.ReceiverTestResults)]++;

                // Adjust seqeunce number from both source to be equal before doing any comparison
                EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);
                DirectMethodTestResult receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);

                long senderSeqNum = this.ConvertStringToLong(senderResult.SequenceNumber);
                long receiverSeqNum = this.ConvertStringToLong(receiverResult.SequenceNumber);

                if (receiverSeqNum > senderSeqNum)
                {
                    // Increment sender result to have the same seq as the receiver
                    await this.IncrementSequenceNumberAsync(
                        this.SenderTestResults,
                        nameof(this.SenderTestResults),
                        receiverSeqNum,
                        resultCount,
                        completedStatusHistogram);

                    // Fail the test
                    isPassing = false;
                }

                if (receiverSeqNum < senderSeqNum)
                {
                    // Increment receiver result to have the same seq as the sender
                    await this.IncrementSequenceNumberAsync(
                        this.ReceiverTestResults,
                        nameof(this.ReceiverTestResults),
                        senderSeqNum,
                        resultCount,
                        completedStatusHistogram);

                    // Fail the test
                    isPassing = false;
                }

                // Note: In the current verification, if the receiver obtained more result dm than
                //   sender, the test is consider a fail. We are disregarding duplicated/unique seqeunce number
                //   of receiver's result.

                // Check if the current dm is passing
                bool isCurrentDirectMethodPassing = true;

                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);
                receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);

                // Verified the sequence numbers are the same
                isCurrentDirectMethodPassing &= senderResult.SequenceNumber == receiverResult.SequenceNumber;

                // Verified the sequence numbers are incremental
                isCurrentDirectMethodPassing &= this.ConvertStringToLong(senderResult.SequenceNumber) > previousSeqNum;
                previousSeqNum = this.ConvertStringToLong(senderResult.SequenceNumber);

                // Verified the BatchId is the same
                isCurrentDirectMethodPassing &= senderResult.BatchId == receiverResult.BatchId;

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                // If the current DM result is passed, increment the count for a good dm
                passedDirectMethodCount += isCurrentDirectMethodPassing ? 1 : 0;
                // Update the overall test status
                isPassing &= isCurrentDirectMethodPassing;

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            // Fail the test
            isPassing &= !(hasSenderResult ^ hasReceiverResult);

            await this.IncrementSequenceNumberAsync(
                this.SenderTestResults,
                nameof(this.SenderTestResults),
                long.MaxValue,
                resultCount,
                completedStatusHistogram);

            await this.IncrementSequenceNumberAsync(
                this.ReceiverTestResults,
                nameof(this.ReceiverTestResults),
                long.MaxValue,
                resultCount,
                completedStatusHistogram);

            return this.CalculateStatistic(
                isPassing,
                passedDirectMethodCount,
                resultCount,
                completedStatusHistogram);
        }

        EdgeHubRestartDirectMethodReport CalculateStatistic(
            bool isPassing,
            long passedDirectMethodCount,
            Dictionary<string, ulong> resultCount,
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

            // Make sure the maximum restart period is within a passable threshold
            isPassing &= maxPeriod < this.Metadata.PassableEdgeHubRestartPeriod;

            return new EdgeHubRestartDirectMethodReport(
                this.TrackingId,
                this.Metadata.TestReportType.ToString(),
                isPassing,
                passedDirectMethodCount,
                resultCount,
                completedStatusHistogram,
                minPeriod,
                maxPeriod,
                medianPeriod,
                meanPeriod,
                variancePeriod);
        }

        async Task IncrementSequenceNumberAsync(
            ITestResultCollection<TestOperationResult> resultCollection,
            string key,
            long targetSequenceNumber,
            Dictionary<string, ulong> resultCount,
            Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            bool isNotEmpty = true;

            EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(resultCollection.Current.Result);
            long seqNum = this.ConvertStringToLong(senderResult.SequenceNumber);

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                resultCount[key]++;

                this.AddEntryToCompletedStatusHistogram(
                    senderResult,
                    completedStatusHistogram);

                isNotEmpty = await resultCollection.MoveNextAsync();
                senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(resultCollection.Current.Result);
                seqNum = this.ConvertStringToLong(senderResult.SequenceNumber);
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
        void AddEntryToCompletedStatusHistogram(
            EdgeHubRestartDirectMethodResult senderResult,
            Dictionary<HttpStatusCode, List<TimeSpan>> histogram)
        {
            HttpStatusCode completedStatus = senderResult.DirectMethodCompletedStatusCode;
            TimeSpan completedPeriod = senderResult.DirectMethodCompletedTime - senderResult.EdgeHubRestartedTime;
            // Try to allocate the list if it is the first time HttpStatusCode shows up
            histogram.TryAdd(completedStatus, new List<TimeSpan>());
            histogram[completedStatus].Add(completedPeriod);
        }

        long ConvertStringToLong(string result)
        {
            long seqNum;
            long.TryParse(result.Split(';').LastOrDefault(), out seqNum);
            return seqNum;
        }
    }
}