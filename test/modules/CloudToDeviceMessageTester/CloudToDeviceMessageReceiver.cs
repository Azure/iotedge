// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    sealed class CloudToDeviceMessageReceiver : CloudToDeviceMessageTesterBase
    {
        readonly string gatewayHostName;
        DeviceClient deviceClient;

        public CloudToDeviceMessageReceiver(
            ILogger logger,
            string iotHubConnectionString,
            string deviceId,
            string moduleId,
            string gatewayHostName,
            TransportType transportType,
            TimeSpan testDuration,
            TestResultReportingClient testResultReportingClient)
            : base(logger, iotHubConnectionString, deviceId, moduleId, transportType, testDuration, testResultReportingClient)
        {
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
        }

        public override void Dispose() => this.deviceClient?.Dispose();

        public async Task ReportTestResult(Message message)
        {
            string trackingId = message.Properties[TestConstants.Message.TrackingIdPropertyName];
            string batchId = message.Properties[TestConstants.Message.BatchIdPropertyName];
            string sequenceNumber = message.Properties[TestConstants.Message.SequenceNumberPropertyName];
            var testResultReceived = new MessageTestResult(this.moduleId + ".receive", DateTime.UtcNow)
            {
                TrackingId = trackingId,
                BatchId = batchId,
                SequenceNumber = sequenceNumber
            };
            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResultReceived);
        }

        public override async Task StartAsync(CancellationToken ct)
        {
            Microsoft.Azure.Devices.RegistryManager registryManager = null;
            try
            {
                registryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(this.iotHubConnectionString);
                await registryManager.AddDeviceAsync(new Microsoft.Azure.Devices.Device(this.deviceId + "_leaf"), ct);
                this.deviceClient = DeviceClient.CreateFromConnectionString(this.iotHubConnectionString + ";" + this.gatewayHostName, this.deviceId, Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only);
                await this.deviceClient.OpenAsync();

                while (!ct.IsCancellationRequested)
                {
                    this.logger.LogInformation("Ready to receive message");
                    try
                    {
                        Message message = await this.deviceClient.ReceiveAsync();
                        this.logger.LogInformation($"Message received. " +
                            $"Sequence Number: {message.Properties[TestConstants.Message.SequenceNumberPropertyName]}, " +
                            $"batchId: {message.Properties[TestConstants.Message.BatchIdPropertyName]}, " +
                            $"trackingId: {message.Properties[TestConstants.Message.TrackingIdPropertyName]}.");
                        await this.ReportTestResult(message);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error occurred while receiving message.");
                    }
                }
            }
            finally
            {
                registryManager?.Dispose();
            }
        }
    }
}
