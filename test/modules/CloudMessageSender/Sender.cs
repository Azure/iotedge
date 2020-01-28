// Copyright (c) Microsoft. All rights reserved.
namespace CloudMessageSender
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    sealed class Sender : IDisposable
    {
        readonly ServiceClient serviceClient;
        readonly ILogger logger;
        readonly string deviceId;
        long messageCount = 0;

        public Sender(
            ServiceClient serviceClient,
            string deviceId,
            ILogger logger)
        {
            this.serviceClient = Preconditions.CheckNotNull(serviceClient, nameof(serviceClient));
            this.logger = logger;
            this.deviceId = deviceId;
        }

        public void Dispose() => this.serviceClient.Dispose();

        public static async Task<Sender> CreateAsync(
            string connectionString,
            TransportType transportType,
            string deviceId,
            ILogger logger)
        {
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString, transportType);
            await serviceClient.OpenAsync();
            return new Sender(
                serviceClient,
                deviceId,
                logger);
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

                return new MessageTestResult(Settings.Current.ModuleId + ".send", DateTime.UtcNow)
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
    }
}
