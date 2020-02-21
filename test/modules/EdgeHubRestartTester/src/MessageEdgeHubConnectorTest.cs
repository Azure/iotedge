// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class MessageEdgeHubConnectorTest : IEdgeHubConnectorTest
    {
        readonly Guid batchId;
        readonly ILogger logger;
        readonly string messageOutputEndpoint;
        long messageCount = 0;
        ModuleClient msgModuleClient = null;
        TestResultReportingClient reportClient = null;

        public MessageEdgeHubConnectorTest(
            Guid batchId,
            ILogger logger,
            ModuleClient msgModuleClient,
            string messageOutputEndpoint)
        {
            this.batchId = batchId;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.msgModuleClient = Preconditions.CheckNotNull(msgModuleClient, nameof(msgModuleClient));
            this.messageOutputEndpoint = Preconditions.CheckNonWhiteSpace(messageOutputEndpoint, nameof(messageOutputEndpoint));
        }

        public async Task StartAsync(
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            CancellationToken cancellationToken)
        {
            (DateTime msgCompletedTime, HttpStatusCode mgsStatusCode) = await this.SendMessageAsync(
                Settings.Current.TrackingId,
                this.batchId,
                this.messageOutputEndpoint,
                runExpirationTime,
                cancellationToken);

            TestResultBase msgTestResult = new EdgeHubRestartMessageResult(
                this.GetSource(),
                DateTime.UtcNow,
                Settings.Current.TrackingId,
                this.batchId.ToString(),
                this.messageCount.ToString(),
                edgeHubRestartedTime,
                msgCompletedTime,
                mgsStatusCode);

            await ModuleUtil.ReportTestResultAsync(
                this.GetReportClient(),
                this.logger,
                msgTestResult,
                cancellationToken);
        }

        TestResultReportingClient GetReportClient()
        {
            if (this.reportClient == null)
            {
                this.reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            }

            return this.reportClient;
        }

        string GetSource() => $"{Settings.Current.ModuleId}.{TestOperationResultType.EdgeHubRestartMessage.ToString()}.{this.messageOutputEndpoint}";

        async Task<Tuple<DateTime, HttpStatusCode>> SendMessageAsync(
            string trackingId,
            Guid batchId,
            string msgOutputEndpoint,
            DateTime runExpirationTime,
            CancellationToken cancellationToken)
        {
            this.messageCount++;

            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { data = DateTime.UtcNow.ToString() })));
                message.Properties.Add("sequenceNumber", this.messageCount.ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                try
                {
                    // Sending the result via edgeHub
                    await this.msgModuleClient.SendEventAsync(msgOutputEndpoint, message);
                    this.logger.LogInformation($"[SendMessageAsync] Send Message with count {this.messageCount}: finished.");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    if (ex is TimeoutException)
                    {
                        // TimeoutException is expected to happen while the EdgeHub is down.
                        // Let's log the attempt and retry the message send until successful
                        this.logger.LogDebug($"[SendMessageAsync] Expected exception caught with SequenceNumber {this.messageCount}, BatchId: {batchId.ToString()}");
                    }
                    else
                    {
                        string errorMessage = $"[SendMessageAsync] Exception caught with SequenceNumber {this.messageCount}, BatchId: {batchId.ToString()}";
                        this.logger.LogError(ex, errorMessage);

                        TestResultBase errorResult = new ErrorTestResult(
                            Settings.Current.TrackingId,
                            this.GetSource(),
                            errorMessage,
                            DateTime.UtcNow);

                        await ModuleUtil.ReportTestResultAsync(
                            this.GetReportClient(),
                            this.logger,
                            errorResult,
                            cancellationToken);
                    }
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }
    }
}