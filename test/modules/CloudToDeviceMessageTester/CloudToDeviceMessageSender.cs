// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    sealed class CloudToDeviceMessageSender : CloudToDeviceMessageTesterBase
    {
        readonly TimeSpan messageDelay;
        readonly TimeSpan testStartDelay;
        readonly string trackingId;
        long messageCount = 0;
        ServiceClient serviceClient;

        public CloudToDeviceMessageSender(
            ILogger logger,
            string iotHubConnectionString,
            string deviceId,
            string moduleId,
            TransportType transportType,
            TimeSpan testDuration,
            TestResultReportingClient testResultReportingClient,
            string trackingId,
            TimeSpan messageDelay,
            TimeSpan testStartDelay)
            : base(logger, iotHubConnectionString, deviceId, moduleId, transportType, testDuration, testResultReportingClient)
        {
            this.messageDelay = messageDelay;
            this.testStartDelay = testStartDelay;
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
        }

        public override void Dispose() => this.serviceClient?.Dispose();

        public override async Task StartAsync(CancellationToken ct)
        {
            this.logger.LogInformation($"CloudToDeviceMessageTester in sender mode with delayed start for {this.testStartDelay}.");
            await Task.Delay(this.testStartDelay, ct);
            DateTime testStartAt = DateTime.UtcNow;

            this.serviceClient = ServiceClient.CreateFromConnectionString(this.iotHubConnectionString, this.transportType);
            await this.serviceClient.OpenAsync();

            Guid batchId = Guid.NewGuid();
            this.logger.LogInformation($"Batch Id={batchId}");

            while (!ct.IsCancellationRequested && this.IsTestTimeUp(testStartAt))
            {
                MessageTestResult testResult = await this.SendCloudToDeviceMessageAsync(batchId, this.trackingId);
                await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResult);
                await Task.Delay(this.messageDelay, ct);
            }
        }

        public async Task<MessageTestResult> SendCloudToDeviceMessageAsync(Guid batchId, string trackingId)
        {
            this.logger.LogInformation($"Sending C2D message to deviceId: {this.deviceId} with Sequence Number: {this.messageCount}, batchId: {batchId}, and trackingId: {trackingId}");
            try
            {
                var message = new Message(Encoding.ASCII.GetBytes("Cloud to device message."));
                message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, this.messageCount.ToString());
                message.Properties.Add(TestConstants.Message.BatchIdPropertyName, batchId.ToString());
                message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, trackingId);
                await this.serviceClient.SendAsync(this.deviceId, message);
                this.messageCount++;

                return new MessageTestResult(this.moduleId + ".send", DateTime.UtcNow)
                {
                    TrackingId = trackingId,
                    BatchId = batchId.ToString(),
                    SequenceNumber = this.messageCount.ToString()
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error occurred while sending Cloud to Device message for Sequence nubmer: {this.messageCount}, batchId: {batchId}.");
                throw ex;
            }
        }

        bool IsTestTimeUp(DateTime testStartAt)
        {
            return (this.testDuration == TimeSpan.Zero) || (DateTime.UtcNow - testStartAt < this.testDuration);
        }
    }
}
