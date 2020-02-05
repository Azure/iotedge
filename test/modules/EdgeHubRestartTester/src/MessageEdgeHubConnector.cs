// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class MessageEdgeHubConnectorTest : IEdgeHubConnector
    {
        readonly ModuleClient msgModuleClient;
        readonly Guid batchId;
        readonly DateTime runExpirationTime;
        readonly CancellationToken cancellationToken;
        readonly DateTime edgeHubRestartedTime;
        readonly ILogger logger;
        long messageCount = 0;

        public MessageEdgeHubConnectorTest(
            ModuleClient msgModuleClient,
            Guid batchId,
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            this.msgModuleClient = Preconditions.CheckNotNull(msgModuleClient, nameof(msgModuleClient));
            this.batchId = batchId;
            this.runExpirationTime = runExpirationTime;
            this.cancellationToken = Preconditions.CheckNotNull(cancellationToken, nameof(cancellationToken));
            this.edgeHubRestartedTime = edgeHubRestartedTime;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public async Task StartAsync()
        {
            (DateTime msgCompletedTime, HttpStatusCode mgsStatusCode) = await this.SendMessageAsync(
                this.msgModuleClient,
                Settings.Current.TrackingId,
                this.batchId,
                Settings.Current.MessageOutputEndpoint,
                this.runExpirationTime,
                this.cancellationToken);

            TestResultBase msgTestResult = new EdgeHubRestartMessageResult(
                Settings.Current.ModuleId + "." + TestOperationResultType.EdgeHubRestartMessage.ToString(),
                DateTime.UtcNow,
                Settings.Current.TrackingId,
                this.batchId.ToString(),
                Interlocked.Read(ref this.messageCount).ToString(),
                this.edgeHubRestartedTime,
                msgCompletedTime,
                mgsStatusCode);

            var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            await ModuleUtil.ReportTestResultUntilSuccessAsync(
                reportClient,
                this.logger,
                msgTestResult,
                this.cancellationToken);
        }

        async Task<Tuple<DateTime, HttpStatusCode>> SendMessageAsync(
            ModuleClient moduleClient,
            string trackingId,
            Guid batchId,
            string msgOutputEndpoint,
            DateTime runExpirationTime,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.messageCount);
            while ((!cancellationToken.IsCancellationRequested) && (DateTime.UtcNow < runExpirationTime))
            {
                Message message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { data = DateTime.UtcNow.ToString() })));
                message.Properties.Add("sequenceNumber", Interlocked.Read(ref this.messageCount).ToString());
                message.Properties.Add("batchId", batchId.ToString());
                message.Properties.Add("trackingId", trackingId);

                try
                {
                    // Sending the result via edgeHub
                    await moduleClient.SendEventAsync(msgOutputEndpoint, message);
                    this.logger.LogInformation($"[SendMessageAsync] Send Message with count {Interlocked.Read(ref this.messageCount).ToString()}: finished.");
                    return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    if (ex is TimeoutException)
                    {
                        // TimeoutException is expected to happen while the EdgeHub is down.
                        // Let's log the attempt and retry the message send until successful
                        this.logger.LogDebug(ex, $"[SendMessageAsync] Exception caught with SequenceNumber {this.messageCount}, BatchId: {batchId.ToString()};");
                    }
                    else
                    {
                        this.logger.LogError(ex, $"[SendMessageAsync] Exception caught with SequenceNumber {this.messageCount}, BatchId: {batchId.ToString()};");
                    }
                }
            }

            return new Tuple<DateTime, HttpStatusCode>(DateTime.UtcNow, HttpStatusCode.InternalServerError);
        }
    }
}