// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public AmqpLinkSettings Settings => this.AmqpLink.Settings;

        public AmqpObjectState State => this.AmqpLink.State;

        protected AmqpLink AmqpLink { get; }

        public Task CloseAsync(TimeSpan timeout) => this.AmqpLink.CloseAsync(timeout);

        public bool IsCbsLink() => this.AmqpLink.IsReceiver
            ? ((Target)this.AmqpLink.Settings.Target).Address.ToString().StartsWith(CbsConstants.CbsAddress, StringComparison.OrdinalIgnoreCase)
            : ((Source)this.AmqpLink.Settings.Source).Address.ToString().StartsWith(CbsConstants.CbsAddress, StringComparison.OrdinalIgnoreCase);

        public void SafeAddClosed(EventHandler handler) => this.AmqpLink.SafeAddClosed(handler);
    }

    public class EdgeReceivingAmqpLink : EdgeAmqpLink, IReceivingAmqpLink
    {
        public EdgeReceivingAmqpLink(ReceivingAmqpLink amqpLink)
            : base(amqpLink)
        {
        }

        public void DisposeMessage(AmqpMessage amqpMessage, Outcome outcome, bool settled, bool batchable) =>
            ((ReceivingAmqpLink)this.AmqpLink).DisposeMessage(amqpMessage, outcome, settled, batchable);

        public void RegisterMessageListener(Action<AmqpMessage> onMessageReceived) => ((ReceivingAmqpLink)this.AmqpLink).RegisterMessageListener(onMessageReceived);
    }

    public class EdgeSendingAmqpLink : EdgeAmqpLink, ISendingAmqpLink
    {
        public EdgeSendingAmqpLink(SendingAmqpLink amqpLink)
            : base(amqpLink)
        {
        }

        public void DisposeDelivery(Delivery delivery, bool settled, Outcome outcome)
            => ((SendingAmqpLink)this.AmqpLink).DisposeDelivery(delivery, settled, outcome);

        public void RegisterCreditListener(Action<uint, bool, ArraySegment<byte>> creditListener)
            => ((SendingAmqpLink)this.AmqpLink).RegisterCreditListener(creditListener);

        public void RegisterDispositionListener(Action<Delivery> deviceDispositionListener)
            => ((SendingAmqpLink)this.AmqpLink).RegisterDispositionListener(deviceDispositionListener);

        public Task SendMessageAsync(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId, TimeSpan timeout)
            => ((SendingAmqpLink)this.AmqpLink).SendMessageAsync(message, deliveryTag, txnId, timeout);

        public void SendMessageNoWait(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId)
            => ((SendingAmqpLink)this.AmqpLink).SendMessageNoWait(message, deliveryTag, txnId);
    }
}
