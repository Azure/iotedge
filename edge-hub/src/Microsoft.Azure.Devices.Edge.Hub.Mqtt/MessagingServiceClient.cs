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
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        readonly IDeviceListener deviceListener;
        readonly IMessageConverter<IProtocolGatewayMessage> messageConverter;

        public MessagingServiceClient(IDeviceListener deviceListener, IMessageConverter<IProtocolGatewayMessage> messageConverter)
        {
            this.deviceListener = Preconditions.CheckNotNull(deviceListener, nameof(deviceListener));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
        }

        public IProtocolGatewayMessage CreateMessage(string address, IByteBuffer payload)
        {
            var message = new ProtocolGatewayMessage(payload, address);
            return message;
        }

        public void BindMessagingChannel(IMessagingChannel<IProtocolGatewayMessage> channel)
        {
            IDeviceProxy deviceProxy = new DeviceProxy(Preconditions.CheckNotNull(channel, nameof(channel)), this.deviceListener.Identity, this.messageConverter);
            this.deviceListener.BindDeviceProxy(deviceProxy);
        }

        static bool IsTwinAddress(string topicName) => topicName.StartsWith(Constants.ServicePrefix, StringComparison.Ordinal);

        public async Task SendAsync(IProtocolGatewayMessage message)
        {
            if (IsTwinAddress(Preconditions.CheckNonWhiteSpace(message.Address, nameof(message.Address))))
            {
                await this.deviceListener.GetTwinAsync();
            }
            else
            {
                Core.IMessage coreMessage = this.messageConverter.ToMessage(Preconditions.CheckNotNull(message, nameof(message)));
                await this.deviceListener.ProcessMessageAsync(coreMessage);
            }
        }

        public Task AbandonAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Abandon);
            return this.deviceListener.ProcessFeedbackMessageAsync(feedBackMessage);
        }

        public Task CompleteAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Complete);
            return this.deviceListener.ProcessFeedbackMessageAsync(feedBackMessage);
        }

        public Task RejectAsync(string messageId)
        {
            MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
            message.SystemProperties.Add(SystemProperties.MessageId, messageId);
            var feedbackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Reject);
            return this.deviceListener.ProcessFeedbackMessageAsync(feedbackMessage);
        }

        public Task DisposeAsync(Exception cause)
        {
            return this.deviceListener.CloseAsync();
        }

        // TODO - Check what value should be set here.
        public int MaxPendingMessages => 100; // From IotHub codebase. 
    }
}