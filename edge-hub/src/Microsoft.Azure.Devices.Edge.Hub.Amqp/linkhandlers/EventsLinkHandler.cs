// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Address matches the template "/devices/{deviceid}/messages/events" or
    /// "/devices/{deviceid}/messages/events"
    /// </summary>
    class EventsLinkHandler : LinkHandler
    {
        static readonly long MaxBatchedMessageSize = 600 * 1024;
        readonly ActionBlock<AmqpMessage> sendMessageProcessor;
        readonly string deviceId;
        readonly string moduleId;

        EventsLinkHandler(IAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
            this.sendMessageProcessor = new ActionBlock<AmqpMessage>(s => this.ProcessMessageAsync(s));
            this.deviceId = this.BoundVariables[Templates.DeviceIdTemplateParameterName];
            this.moduleId = this.BoundVariables.ContainsKey(Templates.ModuleIdTemplateParameterName) ? this.BoundVariables[Templates.ModuleIdTemplateParameterName] : string.Empty;
            Events.Created(this);
        }

        public static ILinkHandler Create(IAmqpLink link, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter)
        {
            if (!link.IsReceiver)
            {
                throw new InvalidOperationException($"Link {requestUri} cannot receive");
            }
            return new EventsLinkHandler(link, requestUri, boundVariables, messageConverter);
        }

        protected override string Name => "Events";

        protected override Task OnOpenAsync(TimeSpan timeout)
        {
            try
            {
                // Override client settle type
                var receivingLink = (IReceivingAmqpLink)this.Link;
                receivingLink.Settings.RcvSettleMode = null; // (byte)ReceiverSettleMode.First (null as it is the default and to avoid bytes on the wire)

                receivingLink.RegisterMessageListener(this.OnMessage);

                receivingLink.SafeAddClosed((s, e) => this.OnReceiveLinkClosed()
                    .ContinueWith(t => Events.ErrorClosingLink(t.Exception, this), TaskContinuationOptions.OnlyOnFaulted));
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Events.ErrorOpeningLink(e, this);
                throw;
            }
        }

        Task OnReceiveLinkClosed()
        {
            this.sendMessageProcessor.Complete();
            return this.DeviceListener.CloseAsync();
        }

        void OnMessage(AmqpMessage amqpMessage) => this.sendMessageProcessor.Post(amqpMessage);

        async Task ProcessMessageAsync(AmqpMessage amqpMessage)
        {
            if (this.Link.State != AmqpObjectState.Opened)
            {
                Events.InvalidLinkState(this);
                return;
            }

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

                ((IReceivingAmqpLink)this.Link).DisposeMessage(amqpMessage, AmqpConstants.AcceptedOutcome, true, true);
                amqpMessage.Dispose();
            }
            catch (Exception e) when (!e.IsFatal())
            {
                this.HandleException(e, amqpMessage, messages);
            }
        }

        void AddMessageSystemProperties(IMessage message)
        {
            if (!string.IsNullOrWhiteSpace(this.deviceId))
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.deviceId;
            }

            if (!string.IsNullOrWhiteSpace(this.moduleId))
            {
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleId;
            }
        }

        static IList<AmqpMessage> ExpandBatchedMessage(AmqpMessage message)
        {
            var outputMessages = new List<AmqpMessage>();

            if (message.DataBody != null)
            {
                foreach (Data data in message.DataBody)
                {
                    AmqpMessage debatchedMessage = AmqpMessage.Create(data);
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

        protected override void OnLinkClosed(object sender, EventArgs args)
        {
            base.OnLinkClosed(sender, args);
            Events.Closed(this);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EventsLinkHandler>();
            const int IdStart = AmqpEventIds.EventsLinkHandler;

            enum EventIds
            {
                Created = IdStart,
                Closed,
                InvalidLinkState,
                ErrorSending,
                ErrorClosing,
                ErrorOpening
            }

            static string GetClientId(EventsLinkHandler handler) => handler.deviceId + (!string.IsNullOrEmpty(handler.moduleId) ? $":{handler.moduleId}" : string.Empty);

            public static void Created(EventsLinkHandler handler)
            {
                Log.LogDebug((int)EventIds.Created, $"New EventsLinkHandler link created for {GetClientId(handler)}");
            }

            public static void Closed(EventsLinkHandler handler)
            {
                Log.LogDebug((int)EventIds.Closed, $"EventsLinkHandler link closed for {GetClientId(handler)}");
            }

            public static void ErrorSending(Exception exception, EventsLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorSending, exception, $"Error opening events link for {GetClientId(handler)}");
            }

            public static void InvalidLinkState(EventsLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.InvalidLinkState, $"Cannot send messages when link state is {handler.Link.State} for {GetClientId(handler)}");
            }

            public static void ErrorClosingLink(AggregateException exception, EventsLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorClosing, exception, $"Error closing events link for {GetClientId(handler)}");
            }

            public static void ErrorOpeningLink(Exception exception, EventsLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorOpening, exception, $"Error opening events link for {GetClientId(handler)}");
            }
        }
    }
}
