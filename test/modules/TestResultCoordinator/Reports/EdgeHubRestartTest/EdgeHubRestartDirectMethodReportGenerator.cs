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
        private Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram;
        delegate Task<(ulong count, bool isNotEmpty, long sequenceNumber)> MoveNextResultAsync(ulong count);

        internal EdgeHubRestartDirectMethodReportGenerator(
            string trackingId,
            string senderSource,
            string receiverSource,
            TestReportType testReportType,
            ITestResultCollection<TestOperationResult> senderTestResults,
            ITestResultCollection<TestOperationResult> receiverTestResults,
            TimeSpan passableEdgeHubRestartPeriod)
        {
            Preconditions.CheckRange(passableEdgeHubRestartPeriod.Ticks, 0);

            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = Preconditions.CheckNonWhiteSpace(receiverSource, nameof(receiverSource));
            this.TestReportType = testReportType;
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverTestResults = Preconditions.CheckNotNull(receiverTestResults, nameof(receiverTestResults));
            this.PassableEdgeHubRestartPeriod = passableEdgeHubRestartPeriod;
        }

        internal string TrackingId { get; }

        internal string SenderSource { get; }

        internal string ReceiverSource { get; }

        internal TestReportType TestReportType { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        internal TimeSpan PassableEdgeHubRestartPeriod { get; }

        private async Task<(ulong resultCount, bool isNotEmpty, long sequenceNumber)> MoveNextSenderResultAsync(ulong senderResultCount)
        {
            bool isNotEmpty = await this.SenderTestResults.MoveNextAsync();
            long seqNum = 0;
            if (isNotEmpty)
            {
                senderResultCount++;

                EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);
                seqNum = this.ConvertStringToLong(senderResult.SequenceNumber);

                this.AddEntryToCompletedStatusHistogram(senderResult);
            }

            return (resultCount: senderResultCount,
                isNotEmpty: isNotEmpty,
                sequenceNumber: seqNum);
        }

        private async Task<(ulong resultCount, bool isNotEmpty, long sequenceNumber)> MoveNextReceiverResultAsync(ulong receiverResultCount)
        {
            bool isNotEmpty = await this.ReceiverTestResults.MoveNextAsync();
            long seqNum = 0;
            if (isNotEmpty)
            {
                receiverResultCount++;

                DirectMethodTestResult receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);
                seqNum = this.ConvertStringToLong(receiverResult.SequenceNumber);
            }

            return (resultCount: receiverResultCount,
                isNotEmpty: isNotEmpty,
                sequenceNumber: seqNum);
        }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Generating report: {nameof(EdgeHubRestartDirectMethodReport)} for [{this.SenderSource}] and [{this.ReceiverSource}]");

            bool isPassing = true;
            long previousSeqNum = 0;
            ulong passedDirectMethodCount = 0;
            ulong senderResultCount = 0;
            ulong receiverResultCount = 0;

            // Value: (completedStatusCode, DirectMethodCompletedTime - EdgeHubRestartedTime)
            //Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram = new Dictionary<HttpStatusCode, List<TimeSpan>>();

//            bool hasSenderResult = await this.SenderTestResults.MoveNextAsync();  BEARWASHERE
            bool hasSenderResult = true;
            long senderSeqNum = 0;

            (senderResultCount, hasSenderResult, senderSeqNum) =
                await this.MoveNextSenderResultAsync(senderResultCount);

// bool hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            bool hasReceiverResult = true;
            long receiverSeqNum = 0;
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

                // Both sender & receiver have their dm results
                //senderResultCount++;   BEARWASHERE
                //receiverResultCount++;

                // Adjust seqeunce number from both source to be equal before doing any comparison
//EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);   BEARWASHERE
//DirectMethodTestResult receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);

                //long senderSeqNum = this.ConvertStringToLong(senderResult.SequenceNumber); BEARWASHERE
                //long receiverSeqNum = this.ConvertStringToLong(receiverResult.SequenceNumber);

                if (receiverSeqNum > senderSeqNum)
                {
                    // BEARWASHERE -- Send an increment delegate to the function for the iteration
                    // Increment sender result to have the same seq as the receiver
                    (senderResultCount, hasSenderResult) = await this.IncrementSequenceNumberAsync(
                        this.MoveNextSenderResultAsync,
                        receiverSeqNum,
                        senderResultCount);

                    // Fail the test
                    isPassing = false;
                }

                if (receiverSeqNum < senderSeqNum)
                {
                    // Increment receiver result to have the same seq as the sender
                    (receiverResultCount, hasReceiverResult) = await this.IncrementSequenceNumberAsync(
                        this.MoveNextReceiverResultAsync,
                        senderSeqNum,
                        receiverResultCount);

                    // Fail the test
                    isPassing = false;
                }

                if (hasSenderResult ^ hasReceiverResult)
                {
                    break;
                }

                // Note: In the current verification, if the receiver obtained more result dm than
                //   sender, the test is consider a fail. We are disregarding duplicated/unique seqeunce number
                //   of receiver's result.

                // Check if the current dm is passing
                bool isCurrentDirectMethodPassing = true;

                EdgeHubRestartDirectMethodResult senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(this.SenderTestResults.Current.Result);
                DirectMethodTestResult receiverResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);

                // Verified the sequence numbers are the same
                isCurrentDirectMethodPassing &= senderResult.SequenceNumber == receiverResult.SequenceNumber;

                // Verified the sequence numbers are incremental
                isCurrentDirectMethodPassing &= this.ConvertStringToLong(senderResult.SequenceNumber) > previousSeqNum;
                previousSeqNum = this.ConvertStringToLong(senderResult.SequenceNumber);

                this.AddEntryToCompletedStatusHistogram(senderResult);

                // If the current DM result is passed, increment the count for a good dm
                passedDirectMethodCount += isCurrentDirectMethodPassing ? 1UL : 0UL;
                // Update the overall test status
                isPassing &= isCurrentDirectMethodPassing;

                hasSenderResult = await this.SenderTestResults.MoveNextAsync();
                hasReceiverResult = await this.ReceiverTestResults.MoveNextAsync();
            }

            // Fail the test
            isPassing &= !(hasSenderResult ^ hasReceiverResult);

            (senderResultCount, _) = await this.IncrementSequenceNumberAsync(
                this.MoveNextSenderResultAsync,
                long.MaxValue,
                senderResultCount);

            (receiverResultCount, _) = await this.IncrementSequenceNumberAsync(
                this.MoveNextReceiverResultAsync,
                long.MaxValue,
                receiverResultCount);

            return this.CalculateStatistic(
                isPassing,
                passedDirectMethodCount,
                senderResultCount,
                receiverResultCount);
        }

        EdgeHubRestartDirectMethodReport CalculateStatistic(
            bool isPassing,
            ulong passedDirectMethodCount,
            ulong senderResultCount,
            ulong receiverResultCount)
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

            // Make sure the maximum restart period is within a passable threshold
            isPassing &= maxPeriod < this.PassableEdgeHubRestartPeriod;

            return new EdgeHubRestartDirectMethodReport(
                this.TrackingId,
                this.TestReportType.ToString(),
                isPassing,
                passedDirectMethodCount,
                this.SenderSource,
                this.ReceiverSource,
                senderResultCount,
                receiverResultCount,
                this.completedStatusHistogram,
                minPeriod,
                maxPeriod,
                medianPeriod,
                meanPeriod,
                variancePeriodInMilisec);
        }

        async Task<(ulong resultCount, bool isNotEmpty)> IncrementSequenceNumberAsync(
            MoveNextResultAsync MoveNextResultAsync,
            long targetSequenceNumber,
            ulong resultCount)
        {
            bool isNotEmpty = true;
            long seqNum = targetSequenceNumber - 1;

            while ((seqNum < targetSequenceNumber) && isNotEmpty)
            {
                (resultCount, isNotEmpty, seqNum) = await MoveNextResultAsync(resultCount);
            }

            return (resultCount: resultCount, isNotEmpty: isNotEmpty);
        }

        // async Task<(ulong resultCount, bool isNotEmpty)> IncrementSequenceNumberAsync(
        //     bool isNotEmpty,
        //     ITestResultCollection<TestOperationResult> resultCollection,
        //     long targetSequenceNumber,
        //     ulong resultCount)
        // {
        //     long seqNum = targetSequenceNumber;
        //     EdgeHubRestartDirectMethodResult senderResult = null;
        //     if (isNotEmpty)
        //     {
        //         senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(resultCollection.Current.Result);
        //         seqNum = this.ConvertStringToLong(senderResult.SequenceNumber);
        //     }

        //     while ((seqNum < targetSequenceNumber) && isNotEmpty)
        //     {
        //         resultCount++;

        //         this.AddEntryToCompletedStatusHistogram(senderResult);

        //         isNotEmpty = await resultCollection.MoveNextAsync();

        //         if (isNotEmpty)
        //         {
        //             senderResult = JsonConvert.DeserializeObject<EdgeHubRestartDirectMethodResult>(resultCollection.Current.Result);
        //             seqNum = this.ConvertStringToLong(senderResult.SequenceNumber);
        //         }
        //     }

        //     return (resultCount: resultCount, isNotEmpty: isNotEmpty);
        // }

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
        void AddEntryToCompletedStatusHistogram(EdgeHubRestartDirectMethodResult senderResult)
        {
            HttpStatusCode completedStatus = senderResult.DirectMethodCompletedStatusCode;
            TimeSpan completedPeriod = senderResult.DirectMethodCompletedTime - senderResult.EdgeHubRestartedTime;
            // Try to allocate the list if it is the first time HttpStatusCode shows up
            this.completedStatusHistogram.TryAdd(completedStatus, new List<TimeSpan>());
            this.completedStatusHistogram[completedStatus].Add(completedPeriod);
        }

        long ConvertStringToLong(string result)
        {
            long seqNum;
            long.TryParse(result.Split(';').LastOrDefault(), out seqNum);
            return seqNum;
        }
    }
}