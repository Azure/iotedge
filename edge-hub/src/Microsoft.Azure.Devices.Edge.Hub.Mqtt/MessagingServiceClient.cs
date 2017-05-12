// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using IPgMessage = ProtocolGateway.Messaging.IMessage;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        readonly IDeviceListener deviceListener;
        readonly IMessageConverter<IPgMessage> messageConverter;

        public MessagingServiceClient(IDeviceListener deviceListener, IMessageConverter<IPgMessage> messageConverter)
        {
            this.deviceListener = Preconditions.CheckNotNull(deviceListener, nameof(deviceListener));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
        }

        public IPgMessage CreateMessage(string address, IByteBuffer payload)
        {
            var message = new PgMessage(payload, address);
            return message;
        }

        public void BindMessagingChannel(IMessagingChannel<IPgMessage> channel)
        {
            IDeviceProxy deviceProxy = new DeviceProxy(Preconditions.CheckNotNull(channel, nameof(channel)), this.deviceListener.Identity, this.messageConverter);
            this.deviceListener.BindDeviceProxy(deviceProxy);
        }

        static bool IsTwinAddress(string topicName) => topicName.StartsWith(Constants.ServicePrefix, StringComparison.Ordinal);

        public async Task SendAsync(IPgMessage message)
        {
            if (!IsTwinAddress(Preconditions.CheckNonWhiteSpace(message.Address, nameof(message.Address))))
            {
                Core.IMessage coreMessage = this.messageConverter.ToMessage(Preconditions.CheckNotNull(message, nameof(message)));
                await this.deviceListener.ReceiveMessage(coreMessage);
            }
        }

        public Task AbandonAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Abandon);
            return this.deviceListener.ReceiveFeedbackMessage(feedBackMessage);
        }

        public Task CompleteAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Complete);
            return this.deviceListener.ReceiveFeedbackMessage(feedBackMessage);
        }

        public Task RejectAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedbackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Reject);
            return this.deviceListener.ReceiveFeedbackMessage(feedbackMessage);
        }

        public Task DisposeAsync(Exception cause)
        {
            return this.deviceListener.CloseAsync();
        }

        // TODO - Check what value should be set here.
        public int MaxPendingMessages => 100; // From IotHub codebase. 
    }
}