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
    /// Base class for all link handlers that send messages to connected devices/modules
    /// </summary>
    public abstract class SendingLinkHandler : LinkHandler, ISendingLinkHandler
    {
        readonly ActionBlock<Delivery> deliveryMessageProcessor;

        protected SendingLinkHandler(ISendingAmqpLink link, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
            Preconditions.CheckArgument(!link.IsReceiver, $"Link {requestUri} cannot send");
            this.SendingAmqpLink = link;
            this.deliveryMessageProcessor = new ActionBlock<Delivery>(this.DisposeMessageAsync);
        }

        protected ISendingAmqpLink SendingAmqpLink { get; }

        protected abstract QualityOfService QualityOfService { get; }

        protected override Task OnOpenAsync(TimeSpan timeout)
        {
            switch (this.QualityOfService)
            {
                case QualityOfService.ExactlyOnce:
                    // SenderSettleMode.Unsettled (null as it is the default and to avoid bytes on the wire)
                    this.SendingAmqpLink.Settings.SndSettleMode = null;
                    // The receiver will only settle after sending the disposition to the sender and receiving a disposition indicating settlement of the delivery from the sender.
                    this.SendingAmqpLink.Settings.RcvSettleMode = (byte)ReceiverSettleMode.Second;
                    this.SendingAmqpLink.RegisterDispositionListener(this.DispositionListener);
                    break;

                case QualityOfService.AtLeastOnce:
                    // SenderSettleMode.Unsettled (null as it is the default and to avoid bytes on the wire)
                    this.SendingAmqpLink.Settings.SndSettleMode = null;
                    // The Receiver will spontaneously settle all incoming transfers.
                    this.SendingAmqpLink.Settings.RcvSettleMode = (byte)ReceiverSettleMode.First;
                    this.SendingAmqpLink.RegisterDispositionListener(this.DispositionListener);
                    break;

                case QualityOfService.AtMostOnce:
                    // The Receiver will spontaneously settle all incoming transfers.
                    this.SendingAmqpLink.Settings.RcvSettleMode = (byte)ReceiverSettleMode.First;
                    // The Sender will send all deliveries settled to the receiver. 
                    this.SendingAmqpLink.Settings.SndSettleMode = (byte)SenderSettleMode.Settled;
                    break;
            }
            return Task.CompletedTask;
        }

        public Task SendMessage(IMessage message)
        {
            if (this.Link.State != AmqpObjectState.Opened)
            {
                Events.InvalidLinkState(this);
                return Task.CompletedTask;
            }

            try
            {
                AmqpMessage amqpMessage = this.MessageConverter.FromMessage(message);
                ArraySegment<byte> deliveryTag = amqpMessage.DeliveryTag.Count == 0
                    ? new ArraySegment<byte>(Guid.NewGuid().ToByteArray())
                    : amqpMessage.DeliveryTag;
                if (this.QualityOfService != QualityOfService.AtMostOnce)
                {
                    this.SendingAmqpLink.SendMessageNoWait(amqpMessage, deliveryTag, AmqpConstants.NullBinary);
                }
                else
                {
                    return this.SendingAmqpLink.SendMessageAsync(amqpMessage, deliveryTag, AmqpConstants.NullBinary, Amqp.Constants.DefaultTimeout);
                }
                Events.MessageSent(this, message);
            }
            catch (Exception ex)
            {
                Events.ErrorProcessingMessage(ex, this);
            }
            return Task.CompletedTask;
        }

        internal void DispositionListener(Delivery delivery) => this.deliveryMessageProcessor.Post(delivery);

        async Task DisposeMessageAsync(Delivery delivery)
        {
            try
            {
                Preconditions.CheckNotNull(delivery, nameof(delivery));
                Preconditions.CheckArgument(delivery.State != null, "Delivery.State should not be null");

                string lockToken = new Guid(delivery.DeliveryTag.Array).ToString();
                FeedbackStatus feedbackStatus = GetFeedbackStatus(delivery);
                await this.DeviceListener.ProcessMessageFeedbackAsync(lockToken, feedbackStatus);
                this.SendingAmqpLink.DisposeDelivery(delivery, true, new Accepted());
            }
            catch (Exception ex)
            {
                Events.ErrorDisposingMessage(ex, this);
            }
        }

        internal static FeedbackStatus GetFeedbackStatus(Delivery delivery)
        {
            if (delivery.State.DescriptorCode == AmqpConstants.AcceptedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Complete;
            }
            else if (delivery.State.DescriptorCode == AmqpConstants.RejectedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Reject;
            }
            else if (delivery.State.DescriptorCode == AmqpConstants.ReleasedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Abandon;
            }
            else
            {
                throw new InvalidOperationException($"Unknown disposition outcome - {delivery.State.DescriptorCode}");
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<SendingLinkHandler>();
            const int IdStart = AmqpEventIds.SendingLinkHandler;

            enum EventIds
            {
                InvalidLinkState = IdStart,
                MessageSent,
                ErrorProcessing,
                ErrorDisposing
            }

            public static void InvalidLinkState(SendingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.InvalidLinkState, $"Cannot send messages when {handler.Type} link state is {handler.Link.State} for {handler.ClientId}");
            }

            internal static void ErrorProcessingMessage(Exception e, SendingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorProcessing, e, $"Error processing message in {handler.Type} link for {handler.ClientId}");
            }

            public static void ErrorDisposingMessage(Exception e, SendingLinkHandler handler)
            {
                Log.LogWarning((int)EventIds.ErrorDisposing, e, $"Error disposing message in {handler.Type} link for {handler.ClientId}");
            }

            public static void MessageSent(SendingLinkHandler handler, IMessage message)
            {
                string GetMessageId()
                {
                    if (!message.SystemProperties.TryGetNonEmptyValue(SystemProperties.LockToken, out string messageId)
                        && !message.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageId, out messageId))
                    {
                        messageId = string.Empty;
                    }
                    return messageId;
                }
                Log.LogDebug((int)EventIds.MessageSent, $"Sent message with id {GetMessageId()} to device {handler.ClientId}");
            }
        }
    }
}
