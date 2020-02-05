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

    class MessageEdgeHubConnectorTest : IEdgeHubConnector, IDisposable
    {
        readonly Guid batchId;
        readonly ILogger logger;
        long messageCount = 0;
        ModuleClient msgModuleClient;

        public MessageEdgeHubConnectorTest(
            Guid batchId,
            ILogger logger)
        {
            this.batchId = batchId;
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public void Dispose() => this.msgModuleClient?.Dispose();

        public async Task StartAsync(
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            CancellationToken cancellationToken)
        {
            (DateTime msgCompletedTime, HttpStatusCode mgsStatusCode) = await this.SendMessageAsync(
                await this.GetModuleClientAsync(),
                Settings.Current.TrackingId,
                this.batchId,
                Settings.Current.MessageOutputEndpoint,
                runExpirationTime,
                cancellationToken);

            TestResultBase msgTestResult = new EdgeHubRestartMessageResult(
                Settings.Current.ModuleId + "." + TestOperationResultType.EdgeHubRestartMessage.ToString(),
                DateTime.UtcNow,
                Settings.Current.TrackingId,
                this.batchId.ToString(),
                this.messageCount.ToString(),
                edgeHubRestartedTime,
                msgCompletedTime,
                mgsStatusCode);

            var reportClient = new TestResultReportingClient { BaseUrl = Settings.Current.ReportingEndpointUrl.AbsoluteUri };
            await ModuleUtil.ReportTestResultAsync(
                reportClient,
                this.logger,
                msgTestResult,
                cancellationToken);
        }

        async Task<ModuleClient> GetModuleClientAsync()
        {
            if (this.msgModuleClient == null)
            {
                this.msgModuleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    this.logger);

                this.msgModuleClient.OperationTimeoutInMilliseconds = (uint)Settings.Current.SdkOperationTimeout.TotalMilliseconds;
            }

            return this.msgModuleClient;
        }

        async Task<Tuple<DateTime, HttpStatusCode>> SendMessageAsync(
            ModuleClient moduleClient,
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
                    await moduleClient.SendEventAsync(msgOutputEndpoint, message);
                    this.logger.LogInformation($"[SendMessageAsync] Send Message with count {this.messageCount.ToString()}: finished.");
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