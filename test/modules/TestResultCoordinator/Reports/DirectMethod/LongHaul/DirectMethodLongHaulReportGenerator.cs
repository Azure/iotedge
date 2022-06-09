// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
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

    class DirectMethodLongHaulReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodLongHaulReportGenerator));

        readonly string trackingId;

        internal DirectMethodLongHaulReportGenerator(
            string testDescription,
            string trackingId,
            string senderSource,
            Topology topology,
            bool mqttBrokerEnabled,
            IAsyncEnumerator<TestOperationResult> senderTestResults,
            string receiverSource,
            IAsyncEnumerator<TestOperationResult> receiverTestResults,
            string resultType)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.Topology = topology;
            this.MqttBrokerEnabled = mqttBrokerEnabled;
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
            this.ReceiverSource = receiverSource;
            this.ReceiverTestResults = receiverTestResults;
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        internal string ReceiverSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ReceiverTestResults { get; }

        internal string SenderSource { get; }

        internal IAsyncEnumerator<TestOperationResult> SenderTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal Topology Topology { get; }

        internal bool MqttBrokerEnabled { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            long statusCodeZero = 0;
            long senderSuccesses = 0;
            long unauthorized = 0;
            long deviceNotFound = 0;
            long transientError = 0;
            long resourceError = 0;
            long notImplemented = 0;
            long receiverSuccesses = 0;
            Dictionary<HttpStatusCode, long> other = new Dictionary<HttpStatusCode, long>();
            while (await this.SenderTestResults.MoveNextAsync())
            {
                this.ValidateDataSource(this.SenderTestResults.Current, this.SenderSource);
                DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.SenderTestResults.Current.Result);
                HttpStatusCode statusCode = dmSenderTestResult.HttpStatusCode;
                switch ((int)statusCode)
                {
                    case 0:
                        statusCodeZero++;
                        break;
                    case 200:
                        senderSuccesses++;
                        break;
                    case 401:
                        unauthorized++;
                        break;
                    case 404:
                        deviceNotFound++;
                        break;
                    case 424:
                        transientError++;
                        break;
                    case 503:
                        resourceError++;
                        break;
                    case 501:
                        notImplemented++;
                        break;
                    default:
                        if (other.ContainsKey(statusCode))
                        {
                            other[statusCode]++;
                        }
                        else
                        {
                            other.Add(statusCode, 1);
                        }

                        break;
                }
            }

            long receiverResults = 0;
            while (await this.ReceiverTestResults.MoveNextAsync())
            {
                this.ValidateDataSource(this.ReceiverTestResults.Current, this.ReceiverSource);
                DirectMethodTestResult dmReceiverTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(this.ReceiverTestResults.Current.Result);
                receiverResults++;
            }

            Logger.LogInformation($"Successfully finished creating {nameof(DirectMethodLongHaulReport)} for Sources [{this.SenderSource}] and [{this.ReceiverSource}]");
            receiverSuccesses = receiverResults;
            return new DirectMethodLongHaulReport(
                this.TestDescription,
                this.trackingId,
                this.SenderSource,
                this.ReceiverSource,
                this.ResultType,
                this.Topology,
                this.MqttBrokerEnabled,
                senderSuccesses,
                receiverSuccesses,
                statusCodeZero,
                unauthorized,
                deviceNotFound,
                transientError,
                resourceError,
                notImplemented,
                other);
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
