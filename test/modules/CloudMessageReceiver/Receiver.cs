// Copyright (c) Microsoft. All rights reserved.
namespace CloudMessageReceiver
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Receiver
    {
        IConfiguration configuration;
        ILogger logger;
        DeviceClient deviceClient;
        TestResultReportingClient testResultReportingClient;

        public Receiver(
            ILogger logger,
            IConfiguration configuration)
        {
            Preconditions.CheckNotNull(logger, nameof(logger));
            Preconditions.CheckNotNull(configuration, nameof(configuration));

            Uri testReportCoordinatorUri = configuration.GetValue<Uri>("ReportingEndpointUrl");
            this.testResultReportingClient = new TestResultReportingClient { BaseUrl = testReportCoordinatorUri.AbsoluteUri };
            this.logger = logger;
            this.configuration = configuration;
        }

        public void Dispose() => this.deviceClient?.Dispose();

        public async Task ReportTestResult(Message message)
        {
            string trackingId = message.Properties[TestConstants.Message.TrackingIdPropertyName];
            string batchId = message.Properties[TestConstants.Message.BatchIdPropertyName];
            string sequenceNumber = message.Properties[TestConstants.Message.SequenceNumberPropertyName];
            var testResultReceived = new MessageTestResult(this.configuration.GetValue<string>("IOTEDGE_MODULEID") + ".receive", DateTime.UtcNow)
            {
                TrackingId = trackingId,
                BatchId = batchId,
                SequenceNumber = sequenceNumber
            };
            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResultReceived);
        }

        public async Task InitAsync(CancellationTokenSource cts)
        {
            this.deviceClient = DeviceClient.CreateFromConnectionString(
                this.configuration.GetValue<string>("DeviceConnectionString"),
                this.configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only));
            await this.deviceClient.OpenAsync();
            while (!cts.IsCancellationRequested)
            {
                this.logger.LogInformation("Ready to receive message");
                Message message = await this.deviceClient.ReceiveAsync();
                this.logger.LogInformation($"Message received. " +
                    $"Sequence Number: {message.Properties[TestConstants.Message.SequenceNumberPropertyName]}, " +
                    $"batchId: {message.Properties[TestConstants.Message.BatchIdPropertyName]}, " +
                    $"trackingId: {message.Properties[TestConstants.Message.TrackingIdPropertyName]}.");
                await this.ReportTestResult(message);
            }
        }
    }
}
