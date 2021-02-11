// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class DirectMethodLongHaulReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodLongHaulReportGenerator));

        readonly string trackingId;

        internal DirectMethodLongHaulReportGenerator(
            string testDescription,
            string trackingId,
            string senderSource,
            ITestResultCollection<TestOperationResult> senderTestResults,
            string receiverSource,
            ITestResultCollection<TestOperationResult> receiverTestResults,
            string resultType)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = receiverSource;
            this.ReceiverTestResults = receiverTestResults;
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        internal string ReceiverSource { get; }

        internal ITestResultCollection<TestOperationResult> ReceiverTestResults { get; }

        internal string SenderSource { get; }

        internal ITestResultCollection<TestOperationResult> SenderTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            ulong senderSuccesses = 0;
            ulong receiverSuccesses = 0;
            ulong statusCodeZero = 0;
            ulong unknown = 0;
            while (await this.SenderTestResults.MoveNextAsync())
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.SenderTestResults.Current.Result);
                switch ((int)dmSenderTestResult.HttpStatusCode)
                {
                    case 0:
                        statusCodeZero++;
                        break;
                    case 200:
                        senderSuccesses++;
                        break;
                    default:
                        unknown++;
                        break;
                }
            }

            ulong receiverResults = 0;
            while (await this.ReceiverTestResults.MoveNextAsync())
            {
                // ReceiverSource will always be there if ReceiverTestResults is so it's safe to put OrDefault
                this.ValidateDataSource(this.ReceiverTestResults.Current, this.ReceiverSource);
                DirectMethodTestResult dmReceiverTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);
                receiverResults++;
            }

            receiverSuccesses = receiverResults;
            return new DirectMethodLongHaulReport(
                this.TestDescription,
                this.trackingId,
                this.SenderSource,
                this.ReceiverSource,
                this.ResultType,
                senderSuccesses,
                receiverSuccesses,
                statusCodeZero,
                unknown);
        }

        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected it to be '{expectedSource}'.");
            }
        }
    }
}
