// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This class wraps an AmqpLink, and provides similar functionality.
    /// This allows unit testing the components that use it
    /// </summary>
    public abstract class EdgeAmqpLink : IAmqpLink
    {
        protected EdgeAmqpLink(AmqpLink amqpLink)
        {
            this.AmqpLink = Preconditions.CheckNotNull(amqpLink, nameof(amqpLink));
            this.Session = new EdgeAmqpSession(this.AmqpLink.Session);
        }

        public bool IsReceiver => this.AmqpLink.IsReceiver;

        public IAmqpSession Session { get; set; }

        public AmqpObjectState State => this.AmqpLink.State;

        public AmqpLinkSettings Settings => this.AmqpLink.Settings;

        protected AmqpLink AmqpLink { get; }

        public void SafeAddClosed(EventHandler handler) => this.AmqpLink.SafeAddClosed(handler);

        public bool IsCbsLink() => this.AmqpLink.IsReceiver
            ? ((Target)this.AmqpLink.Settings.Target).Address.ToString().StartsWith(CbsConstants.CbsAddress, StringComparison.OrdinalIgnoreCase)
            : ((Source)this.AmqpLink.Settings.Source).Address.ToString().StartsWith(CbsConstants.CbsAddress, StringComparison.OrdinalIgnoreCase);

        public Task CloseAsync(TimeSpan timeout) => this.AmqpLink.CloseAsync(timeout);
    }

    public class EdgeReceivingAmqpLink : EdgeAmqpLink, IReceivingAmqpLink
    {
        public EdgeReceivingAmqpLink(ReceivingAmqpLink amqpLink)
            : base(amqpLink)
        {
        }

        public void RegisterMessageListener(Action<AmqpMessage> onMessageReceived) => ((ReceivingAmqpLink)this.AmqpLink).RegisterMessageListener(onMessageReceived);

        public void DisposeMessage(AmqpMessage amqpMessage, Outcome outcome, bool settled, bool batchable) =>
            ((ReceivingAmqpLink)this.AmqpLink).DisposeMessage(amqpMessage, outcome, settled, batchable);
    }

    public class EdgeSendingAmqpLink : EdgeAmqpLink, ISendingAmqpLink
    {
        public EdgeSendingAmqpLink(SendingAmqpLink amqpLink)
            : base(amqpLink)
        {
        }

        public Task SendMessageAsync(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId, TimeSpan timeout)
            => ((SendingAmqpLink)this.AmqpLink).SendMessageAsync(message, deliveryTag, txnId, timeout);

        public void SendMessageNoWait(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId)
            => ((SendingAmqpLink)this.AmqpLink).SendMessageNoWait(message, deliveryTag, txnId);

        public void RegisterDispositionListener(Action<Delivery> deviceDispositionListener)
            => ((SendingAmqpLink)this.AmqpLink).RegisterDispositionListener(deviceDispositionListener);

        public void RegisterCreditListener(Action<uint, bool, ArraySegment<byte>> creditListener)
            => ((SendingAmqpLink)this.AmqpLink).RegisterCreditListener(creditListener);

        public void DisposeDelivery(Delivery delivery, bool settled, Outcome outcome)
            => ((SendingAmqpLink)this.AmqpLink).DisposeDelivery(delivery, settled, outcome);
    }
}
