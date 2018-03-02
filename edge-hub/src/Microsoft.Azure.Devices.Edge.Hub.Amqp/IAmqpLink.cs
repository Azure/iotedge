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
        void SafeAddClosed(EventHandler handler);

        bool IsReceiver { get; }

        IAmqpSession Session { get; set; }

        AmqpObjectState State { get; }

        bool IsCbsLink();

        AmqpLinkSettings Settings { get; }
    }

    /// <summary>
    /// This interface contains functionality similar to ReceivingAmqpLink. 
    /// Created mainly for testing purposes
    /// </summary>
    public interface IReceivingAmqpLink : IAmqpLink
    {
        void RegisterMessageListener(Action<AmqpMessage> onMessageReceived);

        void DisposeMessage(AmqpMessage amqpMessage, Outcome outcome, bool settled, bool batchable);
    }

    /// <summary>
    /// This interface contains functionality similar to SendingAmqpLink. 
    /// Created mainly for testing purposes
    /// </summary>
    public interface ISendingAmqpLink : IAmqpLink
    {
        Task SendMessageAsync(AmqpMessage response, ArraySegment<byte> getDeliveryTag, ArraySegment<byte> nullBinary, TimeSpan defaultTimeout);
    }
}
