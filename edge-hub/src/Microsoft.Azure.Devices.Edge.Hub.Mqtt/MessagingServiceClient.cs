// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetty.Buffers;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    using static System.FormattableString;

    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class MessagingServiceClient : IMessagingServiceClient
    {
        static readonly StringSegment RequestId = new StringSegment(TwinNames.RequestId);

        readonly IDeviceListener deviceListener;
        readonly IMessageConverter<IProtocolGatewayMessage> messageConverter;
        readonly IByteBufferConverter byteBufferConverter;

        public MessagingServiceClient(IDeviceListener deviceListener, IMessageConverter<IProtocolGatewayMessage> messageConverter, IByteBufferConverter byteBufferConverter)
        {
            this.deviceListener = Preconditions.CheckNotNull(deviceListener, nameof(deviceListener));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.byteBufferConverter = Preconditions.CheckNotNull(byteBufferConverter, nameof(byteBufferConverter));
        }

        public int MaxPendingMessages => 100;

        public Task AbandonAsync(string messageId) => IsValidMessageId(messageId)
            ? this.deviceListener.ProcessMessageFeedbackAsync(messageId, FeedbackStatus.Abandon)
            : Task.CompletedTask;

        public void BindMessagingChannel(IMessagingChannel<IProtocolGatewayMessage> channel)
        {
            IDeviceProxy deviceProxy = new DeviceProxy(channel, this.deviceListener.Identity, this.messageConverter, this.byteBufferConverter);
            this.deviceListener.BindDeviceProxy(deviceProxy);
            Events.BindMessageChannel(this.deviceListener.Identity);
        }

        public Task CompleteAsync(string messageId) => IsValidMessageId(messageId)
            ? this.deviceListener.ProcessMessageFeedbackAsync(messageId, FeedbackStatus.Complete)
            : Task.CompletedTask;

        public IProtocolGatewayMessage CreateMessage(string address, IByteBuffer payload)
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(payload, address)
                .Build();
            return message;
        }

        public Task DisposeAsync(Exception cause)
        {
            Events.Disposing(this.deviceListener.Identity, cause);
            return this.deviceListener.CloseAsync();
        }

        public Task RejectAsync(string messageId) => IsValidMessageId(messageId)
            ? this.deviceListener.ProcessMessageFeedbackAsync(messageId, FeedbackStatus.Reject)
            : Task.CompletedTask;

        public async Task SendAsync(IProtocolGatewayMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));

            using (message)
            {
                Preconditions.CheckNonWhiteSpace(message.Address, nameof(message.Address));
                if (IsTwinAddress(message.Address))
                {
                    await this.ProcessTwinAsync(message);
                }
                else if (IsMethodResponseAddress(message.Address))
                {
                    await this.ProcessMethodResponse(message);
                }
                else
                {
                    await this.ProcessMessageAsync(message);
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

        static bool IsMethodResponseAddress(string topicName) => topicName.StartsWith(Constants.MethodPrefix, StringComparison.Ordinal);

        static bool IsTwinAddress(string topicName) => topicName.StartsWith(Constants.TwinPrefix, StringComparison.Ordinal);

        // We are only interested in non-NULL message IDs which are different than TwinLockToken. A twin
        // message sent out via PG for example will cause a feedback to be generated
        // with TwinLockToken as message ID which is redundant.
        static bool IsValidMessageId(string messageId) => messageId != null && messageId != Constants.TwinLockToken;

        Task ProcessMessageAsync(IProtocolGatewayMessage message)
        {
            try
            {
                IMessage coreMessage = this.messageConverter.ToMessage(message);
                Events.ProcessMessage(this.deviceListener.Identity);
                return this.deviceListener.ProcessDeviceMessageAsync(coreMessage);
            }
            catch (Exception e)
            {
                Events.SendMessageFailed(this.deviceListener.Identity, e);
                return TaskEx.Done;
            }
        }

        Task ProcessMethodResponse(IProtocolGatewayMessage message)
        {
            try
            {
                IMessage coreMessage = this.messageConverter.ToMessage(message);
                this.deviceListener.ProcessMethodResponseAsync(coreMessage);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Events.SendMethodResponseFailed(this.deviceListener.Identity, e);
                return TaskEx.Done;
            }
        }

        async Task ProcessTwinAsync(IProtocolGatewayMessage protocolGatewayMessage)
        {
            var properties = new Dictionary<StringSegment, StringSegment>();
            if (TwinAddressHelper.TryParseOperation(protocolGatewayMessage.Address, properties, out TwinAddressHelper.Operation operation, out StringSegment subresource))
            {
                bool hasCorrelationId = properties.TryGetValue(RequestId, out StringSegment correlationId);

                switch (operation)
                {
                    case TwinAddressHelper.Operation.TwinGetState:
                        EnsureNoSubresource(subresource);

                        if (!hasCorrelationId || correlationId.Length == 0)
                        {
                            throw new InvalidOperationException("Correlation id is missing or empty.");
                        }

                        await this.deviceListener.SendGetTwinRequest(correlationId.ToString());
                        Events.GetTwin(this.deviceListener.Identity);
                        break;

                    case TwinAddressHelper.Operation.TwinPatchReportedState:
                        EnsureNoSubresource(subresource);

                        IMessage forwardMessage = new EdgeMessage.Builder(this.byteBufferConverter.ToByteArray(protocolGatewayMessage.Payload))
                            .Build();
                        await this.deviceListener.UpdateReportedPropertiesAsync(forwardMessage, hasCorrelationId ? correlationId.ToString() : string.Empty);
                        Events.UpdateReportedProperties(this.deviceListener.Identity);
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

        static class Events
        {
            const int IdStart = MqttEventIds.MessagingServiceClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MessagingServiceClient>();

            enum EventIds
            {
                BindMessageChannel = IdStart,
                GetTwin,
                UpdateReportedProperties,
                ProcessMessage,
                Dispose,
                SendMethodResponseFailure,
                SendMessageFailure
            }

            public static void BindMessageChannel(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.BindMessageChannel, Invariant($"Binding message channel for device Id {identity.Id}"));
            }

            public static void Disposing(IIdentity identity, Exception cause)
            {
                Log.LogInformation((int)EventIds.Dispose, Invariant($"Disposing MessagingServiceClient for device Id {identity.Id} because of exception - {cause?.ToString() ?? string.Empty}"));
            }

            public static void GetTwin(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.GetTwin, Invariant($"Getting twin for device Id {identity.Id}"));
            }

            public static void ProcessMessage(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ProcessMessage, Invariant($"Processing message for device Id {identity.Id}"));
            }

            public static void SendMessageFailed(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.SendMessageFailure, Invariant($"Message was not sent for device Id {identity.Id} exception {exception}"));
            }

            public static void SendMethodResponseFailed(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.SendMethodResponseFailure, Invariant($"Methods response was not sent for device Id {identity.Id} exception {exception}"));
            }

            public static void UpdateReportedProperties(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating reported properties for device Id {identity.Id}"));
            }
        }
    }
}
