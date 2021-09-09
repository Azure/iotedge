// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;

    /// <summary>
    /// This interface contains functionality similar to AmqpLink.
    /// This allows unit testing the components that use it
    /// </summary>
    public interface IAmqpLink
    {
        bool IsReceiver { get; }

        IAmqpSession Session { get; set; }

        AmqpObjectState State { get; }

        AmqpLinkSettings Settings { get; }

        void SafeAddClosed(EventHandler handler);

        bool IsCbsLink();

        Task CloseAsync(TimeSpan timeout);
    }

    /// <summary>
    /// This interface contains functionality similar to ReceivingAmqpLink.
    /// Created mainly for testing purposes
    /// </summary>
    public interface IReceivingAmqpLink : IAmqpLink
    {
        void RegisterMessageListener(Action<AmqpMessage> onMessageReceived);

        void DisposeMessage(AmqpMessage message, Outcome outcome, bool settled, bool batchable);
    }

    /// <summary>
    /// This interface contains functionality similar to SendingAmqpLink.
    /// Created mainly for testing purposes
    /// </summary>
    public interface ISendingAmqpLink : IAmqpLink
    {
        Task SendMessageAsync(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId, TimeSpan timeout);

        void SendMessageNoWait(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId);

        void RegisterDispositionListener(Action<Delivery> deviceDispositionListener);

        void RegisterCreditListener(Action<uint, bool, ArraySegment<byte>> creditListener);

        void DisposeDelivery(Delivery delivery, bool settled, Outcome outcome);
    }
}
