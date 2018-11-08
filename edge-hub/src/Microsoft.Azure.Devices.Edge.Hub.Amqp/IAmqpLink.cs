// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        AmqpLinkSettings Settings { get; }

        AmqpObjectState State { get; }

        Task CloseAsync(TimeSpan timeout);

        bool IsCbsLink();

        void SafeAddClosed(EventHandler handler);
    }

    /// <summary>
    /// This interface contains functionality similar to ReceivingAmqpLink.
    /// Created mainly for testing purposes
    /// </summary>
    public interface IReceivingAmqpLink : IAmqpLink
    {
        void DisposeMessage(AmqpMessage message, Outcome outcome, bool settled, bool batchable);

        void RegisterMessageListener(Action<AmqpMessage> onMessageReceived);
    }

    /// <summary>
    /// This interface contains functionality similar to SendingAmqpLink.
    /// Created mainly for testing purposes
    /// </summary>
    public interface ISendingAmqpLink : IAmqpLink
    {
        void DisposeDelivery(Delivery delivery, bool settled, Outcome outcome);

        void RegisterCreditListener(Action<uint, bool, ArraySegment<byte>> creditListener);

        void RegisterDispositionListener(Action<Delivery> deviceDispositionListener);

        Task SendMessageAsync(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId, TimeSpan timeout);

        void SendMessageNoWait(AmqpMessage message, ArraySegment<byte> deliveryTag, ArraySegment<byte> txnId);
    }
}
