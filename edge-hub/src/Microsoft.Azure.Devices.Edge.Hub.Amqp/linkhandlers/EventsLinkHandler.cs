// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Address matches the template "/devices/{deviceid}/messages/events" or
    /// "/devices/{deviceid}/modules/{moduleid}/messages/events"
    /// </summary>
    class EventsLinkHandler : ReceivingLinkHandler
    {
        static readonly long MaxBatchedMessageSize = 600 * 1024;

        public EventsLinkHandler(
            IIdentity identity,
            IReceivingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
        }

        public override LinkType Type => LinkType.Events;

        protected override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        protected override async Task OnMessageReceived(AmqpMessage amqpMessage)
        {
            IList<AmqpMessage> messages = null;
            try
            {
                bool batched = amqpMessage.MessageFormat == AmqpConstants.AmqpBatchedMessageFormat;

                // Enforce coarse message size limit as an optimization to fail fast
                long messageSize = AmqpMessageUtils.GetMessageSize(amqpMessage);
                if (messageSize > MaxBatchedMessageSize)
                {
                    throw new InvalidOperationException($"Message is too large - {messageSize}");
                }

                messages = batched
                    ? ExpandBatchedMessage(amqpMessage)
                    : new List<AmqpMessage>
                    {
                        amqpMessage
                    };

                List<IMessage> outgoingMessages = messages.Select(m => this.MessageConverter.ToMessage(m)).ToList();
                outgoingMessages.ForEach(m => this.AddMessageSystemProperties(m));
                await this.DeviceListener.ProcessDeviceMessageBatchAsync(outgoingMessages);
                Events.ProcessedMessages(messages, this);
            }
            catch (Exception e) when (!e.IsFatal())
            {
                this.HandleException(e, amqpMessage, messages);
            }
        }

        void AddMessageSystemProperties(IMessage message)
        {
            if (this.Identity is IDeviceIdentity deviceIdentity)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = deviceIdentity.DeviceId;
            }

            if (this.Identity is IModuleIdentity moduleIdentity)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = moduleIdentity.ModuleId;
            }
        }

        internal static IList<AmqpMessage> ExpandBatchedMessage(AmqpMessage message)
        {
            var outputMessages = new List<AmqpMessage>();

            if (message.DataBody != null)
            {
                foreach (Data data in message.DataBody)
                {
                    var payload = (ArraySegment<byte>)data.Value;
                    AmqpMessage debatchedMessage = AmqpMessage.CreateAmqpStreamMessage(
                        new BufferListStream(
                            new List<ArraySegment<byte>>()
                            {
                                payload
                            }));
                    outputMessages.Add(debatchedMessage);
                }
            }

            return outputMessages;
        }

        void HandleException(Exception ex, AmqpMessage incoming, IList<AmqpMessage> outgoing)
        {
            // Get AmqpException 
            AmqpException amqpException = AmqpExceptionsHelper.GetAmqpException(ex);
            var rejected = new Rejected { Error = amqpException.Error };
            ((IReceivingAmqpLink)this.Link).DisposeMessage(incoming, rejected, true, true);

            incoming?.Dispose();
            if (outgoing != null)
            {
                foreach (AmqpMessage message in outgoing)
                {
                    message.Dispose();
                }
            }

            Events.ErrorSending(ex, this);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EventsLinkHandler>();
            const int IdStart = AmqpEventIds.EventsLinkHandler;

            enum EventIds
            {
                ProcessedMessages = IdStart,
                ErrorSending
            }

            public static void ErrorSending(Exception ex, EventsLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorSending, ex, $"Error opening events link for {handler.ClientId}");
            }

            internal static void ProcessedMessages(IList<AmqpMessage> messages, EventsLinkHandler handler)
            {
                Log.LogDebug((int)EventIds.ProcessedMessages, $"EventsLinkHandler processed {messages.Count} messages for {handler.ClientId}");
            }
        }
    }
}
