// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class CloudToDeviceMessageSender : ICloudToDeviceMessageTester
    {
        readonly ILogger logger;
        readonly string iotHubConnectionString;
        readonly string deviceId;
        readonly string moduleId;
        readonly TimeSpan testDuration;
        readonly TestResultReportingClient testResultReportingClient;
        readonly TimeSpan messageDelay;
        readonly TimeSpan testStartDelay;
        readonly string trackingId;
        long messageCount = 0;
        ServiceClient serviceClient;

        internal CloudToDeviceMessageSender(
            ILogger logger,
            C2DTestSharedSettings sharedMetadata,
            C2DTestSenderSettings senderMetadata,
            TestResultReportingClient testResultReportingClient)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.iotHubConnectionString = Preconditions.CheckNonWhiteSpace(sharedMetadata.IotHubConnectionString, nameof(sharedMetadata.IotHubConnectionString));
            this.deviceId = Preconditions.CheckNonWhiteSpace(sharedMetadata.DeviceId, nameof(sharedMetadata.DeviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(sharedMetadata.ModuleId, nameof(sharedMetadata.ModuleId));
            this.trackingId = Preconditions.CheckNonWhiteSpace(senderMetadata.TrackingId, nameof(senderMetadata.TrackingId));
            this.messageDelay = senderMetadata.MessageDelay;
            this.testStartDelay = senderMetadata.TestStartDelay;
            this.testDuration = senderMetadata.TestDuration;
            this.testResultReportingClient = Preconditions.CheckNotNull(testResultReportingClient, nameof(testResultReportingClient));
        }

        public void Dispose() => this.serviceClient?.Dispose();

        public async Task StartAsync(CancellationToken ct)
        {
            this.logger.LogInformation($"CloudToDeviceMessageTester in sender mode with delayed start for {this.testStartDelay}.");
            await Task.Delay(this.testStartDelay, ct);
            DateTime testStartAt = DateTime.UtcNow;

            this.serviceClient = ServiceClient.CreateFromConnectionString(this.iotHubConnectionString);
            await this.serviceClient.OpenAsync();

            Guid batchId = Guid.NewGuid();
            this.logger.LogInformation($"Batch Id={batchId}");

            while (!ct.IsCancellationRequested && this.IsTestTimeUp(testStartAt))
            {
                try
                {
                    MessageTestResult testResult = await this.SendCloudToDeviceMessageAsync(batchId, this.trackingId);
                    this.messageCount++;
                    await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResult);
                    await Task.Delay(this.messageDelay, ct);
                }
                catch (DeviceMaximumQueueDepthExceededException ex)
                {
                    this.logger.LogInformation($"Too many messages in IoTHub Queue for Sequence Number: {this.messageCount}, batchId: {batchId}. Error message: {ex.Message}");
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Error occurred while sending Cloud to Device message for Sequence number: {this.messageCount}, batchId: {batchId}. Error message: {ex.Message}");
                    throw;
                }
            }
        }

        internal async Task<MessageTestResult> SendCloudToDeviceMessageAsync(Guid batchId, string trackingId)
        {
            this.logger.LogInformation($"Sending C2D message to deviceId: {this.deviceId} with Sequence Number: {this.messageCount}, batchId: {batchId}, and trackingId: {trackingId}");

            var message = new Message(Encoding.ASCII.GetBytes("Cloud to device message."));
            message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, this.messageCount.ToString());
            message.Properties.Add(TestConstants.Message.BatchIdPropertyName, batchId.ToString());
            message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, trackingId);
            await this.serviceClient.SendAsync(this.deviceId, message);

            return new MessageTestResult(this.moduleId + ".send", DateTime.UtcNow)
            {
                TrackingId = trackingId,
                BatchId = batchId.ToString(),
                SequenceNumber = this.messageCount.ToString()
            };
        }

        bool IsTestTimeUp(DateTime testStartAt)
        {
            return (this.testDuration == TimeSpan.Zero) || (DateTime.UtcNow - testStartAt < this.testDuration);
        }
    }
}
