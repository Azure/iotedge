// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Gateway.Runtime.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using static System.FormattableString;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        static readonly StringSegment RequestId = new StringSegment(TwinNames.RequestId);

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
            Events.BindMessageChannel(this.deviceListener.Identity);
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
                    bool hasCorrelationId = properties.TryGetValue(RequestId, out correlationId);

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
                            Events.GetTwin(this.deviceListener.Identity);
                            break;

                        case TwinAddressHelper.Operation.TwinPatchReportedState:
                            EnsureNoSubresource(subresource);
                            await this.deviceListener.UpdateReportedPropertiesAsync(message.Payload.ToString(System.Text.Encoding.UTF8));
                            if (hasCorrelationId)
                            {
                                MqttMessage mqttMessage = new MqttMessage.Builder(new byte[0])
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
                                Events.UpdateReportedProperties(this.deviceListener.Identity);
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
                Events.SendMessage(this.deviceListener.Identity);
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
            //Reject is not supported by IoTHub on MQTT
            return TaskEx.Done;
        }

        public Task DisposeAsync(Exception cause)
        {
            Events.Disposing(this.deviceListener.Identity, cause);
            return this.deviceListener.CloseAsync();
        }

        // TODO - Check what value should be set here.
        public int MaxPendingMessages => 100; // From IotHub codebase. 

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<MessagingServiceClient>();
            const int IdStart = MqttEventIds.MessagingServiceClient;

            enum EventIds
            {
                BindMessageChannel = IdStart,
                GetTwin,
                UpdateReportedProperties,
                SendMessage,
                Dispose
            }
            
            public static void BindMessageChannel(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.BindMessageChannel, Invariant($"Binding message channel for device Id {identity.Id}"));
            }

            public static void GetTwin(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.GetTwin, Invariant($"Getting twin for device Id {identity.Id}"));
            }

            public static void UpdateReportedProperties(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating reported properties for device Id {identity.Id}"));
            }

            public static void SendMessage(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SendMessage, Invariant($"Sending message for device Id {identity.Id}"));
            }

            public static void Disposing(IIdentity identity, Exception cause)
            {
                Log.LogInformation((int)EventIds.Dispose, Invariant($"Disposing MessagingServiceClient for device Id {identity.Id} because of exception - {cause?.ToString() ?? string.Empty}"));
            }
        }
    }
}