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
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using static System.FormattableString;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        static readonly StringSegment RequestId = new StringSegment(TwinNames.RequestId);
        static readonly string TwinLockToken = "r";

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
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(payload, address)
                .Build();
            return message;
        }

        public void BindMessagingChannel(IMessagingChannel<IProtocolGatewayMessage> channel)
        {
            this.messagingChannel = Preconditions.CheckNotNull(channel, nameof(channel));
            IDeviceProxy deviceProxy = new DeviceProxy(channel, this.deviceListener.Identity, this.messageConverter);
            this.deviceListener.BindDeviceProxy(deviceProxy);
            Events.BindMessageChannel(this.deviceListener.Identity);
        }

        static bool IsTwinAddress(string topicName) => topicName.StartsWith(Constants.TwinPrefix, StringComparison.Ordinal);

        static bool IsMethodResponseAddress(string topicName) => topicName.StartsWith(Constants.MethodPrefix, StringComparison.Ordinal);

        async Task SendTwinAsync(IProtocolGatewayMessage message)
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
                        coreMessage.SystemProperties[SystemProperties.LockToken] = TwinLockToken;
                        coreMessage.SystemProperties[SystemProperties.StatusCode] = ResponseStatusCodes.Ok;
                        coreMessage.SystemProperties[SystemProperties.CorrelationId] = correlationId.ToString();
                        coreMessage.SystemProperties[SystemProperties.OutboundUri] = Constants.OutboundUriTwinEndpoint;
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
                                    [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o"),
                                    [SystemProperties.LockToken] = TwinLockToken,
                                    [SystemProperties.StatusCode] = ResponseStatusCodes.NoContent,
                                    [SystemProperties.CorrelationId] = correlationId.ToString(),
                                    [SystemProperties.OutboundUri] = Constants.OutboundUriTwinEndpoint
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

        Task SendMethodResponse(IProtocolGatewayMessage message)
        {
            try
            {
                Events.SendMethodResponse(this.deviceListener.Identity, message.Address);
                Core.IMessage msg = this.messageConverter.ToMessage(message);

                if (!msg.Properties.TryGetValue(SystemProperties.CorrelationId, out string correlationId)
                    || !msg.Properties.TryGetValue(SystemProperties.StatusCode, out string statusCode)
                    || !int.TryParse(statusCode, out int statusCodeValue))
                {
                    Events.SendMethodResponseInvalid(this.deviceListener.Identity, message.Address);
                    return TaskEx.Done;
                }

                message.Payload.ResetReaderIndex();
                return this.deviceListener.ProcessMethodResponseAsync(new DirectMethodResponse(correlationId, message.Payload.ToByteArray(), statusCodeValue));
            }
            catch (Exception e)
            {
                Events.SendMethodResponseFailed(this.deviceListener.Identity, e);
                return TaskEx.Done;
            }
        }

        Task SendMessageAsync(IProtocolGatewayMessage message)
        {
            try
            {
                Core.IMessage coreMessage = this.messageConverter.ToMessage(message);
                Events.SendMessage(this.deviceListener.Identity);
                return this.deviceListener.ProcessMessageAsync(coreMessage);
            }
            catch (Exception e)
            {
                Events.SendMessageFailed(this.deviceListener.Identity, e);
                return TaskEx.Done;
            }
        }

        public async Task SendAsync(IProtocolGatewayMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));           

            using (message)
            {
                Preconditions.CheckNonWhiteSpace(message.Address, nameof(message.Address));
                if (IsTwinAddress(message.Address))
                {
                    await this.SendTwinAsync(message);
                }
                else if (IsMethodResponseAddress(message.Address))
                {
                    await this.SendMethodResponse(message);
                }
                else
                {
                    await this.SendMessageAsync(message);
                }
            }
        }

        static void EnsureNoSubresource(StringSegment subresource)
        {
            if (subresource.Length != 0)
            {
                throw new InvalidOperationException($"Further resource specialization is not supported: `{subresource.ToString()}`.");
            }
        }

        // We are only interested in non-NULL message IDs which are different than TwinLockToken. A twin
        // message sent out via PG for example will cause a feedback to be generated
        // with TwinLockToken as message ID which is redundant.
        static bool IsValidMessageId(string messageId) => messageId != null && messageId != TwinLockToken;

        public Task AbandonAsync(string messageId)
        {
            if (IsValidMessageId(messageId))
            {
                MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
                message.SystemProperties.Add(SystemProperties.MessageId, messageId);
                var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Abandon);
                return this.deviceListener.ProcessFeedbackMessageAsync(feedBackMessage);
            }
            return TaskEx.Done;
        }

        public Task CompleteAsync(string messageId)
        {
            if (IsValidMessageId(messageId))
            {
                MqttMessage message = new MqttMessage.Builder(new byte[0]).Build();
                message.SystemProperties.Add(SystemProperties.MessageId, messageId);
                var feedBackMessage = new MqttFeedbackMessage(message, FeedbackStatus.Complete);
                return this.deviceListener.ProcessFeedbackMessageAsync(feedBackMessage);
            }
            return TaskEx.Done;
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
                Dispose,
                SendMethodResponse,
                InvalidMethodResponse,
                SendMessageFailure,
                SendMethodResponseFailure
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

            public static void SendMethodResponse(IIdentity identity, string address)
            {
                Log.LogDebug((int)EventIds.SendMethodResponse, Invariant($"Sending method response with address {address} for device Id {identity.Id}"));
            }

            public static void SendMethodResponseInvalid(IIdentity identity, string address)
            {
                Log.LogError((int)EventIds.InvalidMethodResponse, Invariant($"Method response address is invalid {address} for device Id {identity.Id}"));
            }

            public static void Disposing(IIdentity identity, Exception cause)
            {
                Log.LogInformation((int)EventIds.Dispose, Invariant($"Disposing MessagingServiceClient for device Id {identity.Id} because of exception - {cause?.ToString() ?? string.Empty}"));
            }

            public static void SendMessageFailed(IIdentity identity, Exception exception)
            {
                Log.LogError((int)EventIds.SendMessageFailure, Invariant($"Message was not sent for device Id {identity.Id} exception {exception}"));
            }

            public static void SendMethodResponseFailed(IIdentity identity, Exception exception)
            {
                Log.LogError((int)EventIds.SendMethodResponseFailure, Invariant($"Method response was not sent for device Id {identity.Id} exception {exception}"));
            }
        }
    }
}