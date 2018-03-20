// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Base class for all receiving link handlers
    /// </summary>
    public abstract class ReceivingLinkHandler : LinkHandler, IReceivingLinkHandler
    {
        readonly ActionBlock<AmqpMessage> sendMessageProcessor;

        protected ReceivingLinkHandler(IReceivingAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
            Preconditions.CheckArgument(link.IsReceiver, $"Link {requestUri} cannot receive");
            this.ReceivingLink = link;
            this.sendMessageProcessor = new ActionBlock<AmqpMessage>(s => this.ProcessMessageAsync(s));
        }

        protected IReceivingAmqpLink ReceivingLink { get; }

        protected override Task OnOpenAsync(TimeSpan timeout)
        {
            // TODO: Check if these can be set to null to use defaults and avoid bytes on the wire
            // The Receiver will spontaneously settle all incoming transfers.
            this.ReceivingLink.Settings.RcvSettleMode = (byte)ReceiverSettleMode.First;
            // The Sender will send all deliveries settled to the receiver.
            this.ReceivingLink.Settings.SndSettleMode = (byte)SenderSettleMode.Settled;

            this.ReceivingLink.RegisterMessageListener(m => this.sendMessageProcessor.Post(m));
            this.ReceivingLink.SafeAddClosed((s, e) => this.OnReceiveLinkClosed()
                .ContinueWith(t => Events.ErrorClosingLink(t.Exception, this), TaskContinuationOptions.OnlyOnFaulted));

            return Task.CompletedTask;
        }

        Task OnReceiveLinkClosed()
        {
            this.sendMessageProcessor.Complete();
            return Task.CompletedTask;
        }

        protected abstract Task OnMessageReceived(AmqpMessage amqpMessage);

        internal async Task ProcessMessageAsync(AmqpMessage amqpMessage)
        {
            if (this.Link.State != AmqpObjectState.Opened)
            {
                Events.InvalidLinkState(this);
                return;
            }

            try
            {
                await this.OnMessageReceived(amqpMessage);
                ((IReceivingAmqpLink)this.Link).DisposeMessage(amqpMessage, AmqpConstants.AcceptedOutcome, true, true);
                amqpMessage.Dispose();
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.ErrorProcessingMessage(e, this);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ReceivingLinkHandler>();
            const int IdStart = AmqpEventIds.ReceivingLinkHandler;

            enum EventIds
            {
                InvalidLinkState = IdStart,
                ErrorClosing,
                ErrorProcessing
            }

            public static void InvalidLinkState(ReceivingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.InvalidLinkState, $"Cannot send messages when link state is {handler.Link.State} for {handler.ClientId}");
            }

            public static void ErrorClosingLink(AggregateException e, ReceivingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorClosing, e, $"Error closing events link for {handler.ClientId}");
            }

            internal static void ErrorProcessingMessage(Exception e, ReceivingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorProcessing, e, $"Error processing message in {handler.Type} link for {handler.ClientId}");
            }
        }
    }
}
