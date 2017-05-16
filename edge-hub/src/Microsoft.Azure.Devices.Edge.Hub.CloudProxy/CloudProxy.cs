// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class CloudProxy : ICloudProxy
    {
        const int ExceptionEventId = 0;
        readonly DeviceClient deviceClient;
        readonly IMessageConverter<Message> messageConverter;
        readonly ILogger logger;
        readonly AtomicBoolean isActive;
        CloudReceiver cloudReceiver;

        public CloudProxy(DeviceClient deviceClient, IMessageConverter<Message> messageConverter, ILogger logger)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.isActive = new AtomicBoolean(true);
        }

        public async Task<bool> CloseAsync()
        {
            try
            {
                if (this.isActive.GetAndSet(false))
                {
                    if (this.cloudReceiver != null)
                    {
                        await this.cloudReceiver.CloseAsync();
                    }
                    await this.deviceClient.CloseAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ExceptionEventId, ex, "Error closing IoTHub connection");
                return false;
            }
        }

        public Task<Twin> GetTwinAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SendMessageAsync(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Message message = this.messageConverter.FromMessage(inputMessage);

            try
            {
                await this.deviceClient.SendEventAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ExceptionEventId, ex, "Error sending message to IoTHub");
                return false;
            }
        }

        public async Task<bool> SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            IEnumerable<Message> messages = Preconditions.CheckNotNull(inputMessages, nameof(inputMessages))
                .Select(inputMessage => this.messageConverter.FromMessage(inputMessage));
            try
            {
                await this.deviceClient.SendEventBatchAsync(messages);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ExceptionEventId, ex, "Error sending message batch to IoTHub");
                return false;
            }
        }

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
        {
            throw new NotImplementedException();
        }

        public void BindCloudListener(ICloudListener cloudListener)
        {
            this.cloudReceiver = new CloudReceiver(this.deviceClient, this.messageConverter, cloudListener);
            this.cloudReceiver.StarListening();
        }

        public bool IsActive => this.isActive.Get();

        public Task SendFeedbackMessageAsync(IFeedbackMessage message)
        {
            message.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId);
            switch (message.FeedbackStatus)
            {
                case FeedbackStatus.Complete:
                    return this.deviceClient.CompleteAsync(messageId);
                case FeedbackStatus.Abandon:
                    return this.deviceClient.AbandonAsync(messageId);
                case FeedbackStatus.Reject:
                    return this.deviceClient.ReceiveAsync();
                default:
                    throw new InvalidOperationException("Feedback status type is not supported");
            }
        }
    }
}
