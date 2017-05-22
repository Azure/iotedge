// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Gateway.Runtime.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Extensions.Primitives;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;
    using System.Globalization;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        static StringSegment requestId = new StringSegment(TwinNames.RequestId);

        readonly IDeviceListener deviceListener;
        readonly IMessageConverter<IProtocolGatewayMessage> messageConverter;
        IMessagingChannel<IProtocolGatewayMessage> messagingChannel;

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
            this.messagingChannel = Preconditions.CheckNotNull(channel, nameof(channel));
            IDeviceProxy deviceProxy = new DeviceProxy(channel, this.deviceListener.Identity, this.messageConverter);
            this.deviceListener.BindDeviceProxy(deviceProxy);
        }

        static bool IsTwinAddress(string topicName) => topicName.StartsWith(Constants.ServicePrefix, StringComparison.Ordinal);

        public async Task SendAsync(IProtocolGatewayMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));

            if (IsTwinAddress(Preconditions.CheckNonWhiteSpace(message.Address, nameof(message.Address))))
            {
                var properties = new Dictionary<StringSegment, StringSegment>();
                TwinAddressHelper.Operation operation;
                StringSegment subresource;
                if (TwinAddressHelper.TryParseOperation(message.Address, properties, out operation, out subresource))
                {
                    StringSegment correlationId;
                    bool hasCorrelationId = properties.TryGetValue(requestId, out correlationId);

                    switch (operation)
                    {
                        case TwinAddressHelper.Operation.TwinGetState:
                            EnsureNoSubresource(subresource);

                            if (!hasCorrelationId || correlationId.Length == 0)
                            {
                                throw new InvalidOperationException("Correlation id is missing or empty.");
                            }

                            Core.IMessage coreMessage = await this.deviceListener.GetTwinAsync();
                            coreMessage.SystemProperties[Core.SystemProperties.LockToken] = "r";
                            coreMessage.SystemProperties[Core.SystemProperties.StatusCode] = ResponseStatusCodes.OK;
                            coreMessage.SystemProperties[Core.SystemProperties.CorrelationId] = correlationId.ToString();
                            IProtocolGatewayMessage twinGetMessage = this.messageConverter.FromMessage(coreMessage);
                            this.messagingChannel.Handle(twinGetMessage);
                            break;

                        case TwinAddressHelper.Operation.TwinPatchReportedState:
                            EnsureNoSubresource(subresource);
                            await this.deviceListener.UpdateReportedPropertiesAsync(message.Payload.ToString(System.Text.Encoding.UTF8));
                            if (hasCorrelationId)
                            {
                                var mqttMessage = new MqttMessage.Builder(new byte[0])
                                    .SetSystemProperties(new Dictionary<string, string>()
                                    {
                                        [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture),
                                        [SystemProperties.LockToken] = "r",
                                        [SystemProperties.StatusCode] = ResponseStatusCodes.NoContent,
                                        [SystemProperties.CorrelationId] = correlationId.ToString()
                                    })
                                    .Build();
                                IProtocolGatewayMessage twinPatchMessage = this.messageConverter.FromMessage(mqttMessage);
                                this.messagingChannel.Handle(twinPatchMessage);
                            }
                            break;

                        default:
                            throw new InvalidOperationException("Twin operation is not supported.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to parse operation from message address.");
                }
            }
            else
            {
                Core.IMessage coreMessage = this.messageConverter.ToMessage(message);
                await this.deviceListener.ProcessMessageAsync(coreMessage);
            }
        }

        static void EnsureNoSubresource(StringSegment subresource)
        {
            if (subresource.Length != 0)
            {
                throw new InvalidOperationException($"Further resource specialization is not supported: `{subresource.ToString()}`.");
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