// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// If we throw an exception when creating a link the error info doesn't get sent back to the client.
    /// The purpose of this class is to take the exception from Create, give the session a link, and then
    /// throw the Exception when trying to Open the link (so error details do get sent over the wire).
    /// </summary>
    public class FaultedLink : AmqpLink
    {
        public FaultedLink(Exception exception, AmqpSession session, AmqpLinkSettings linkSettings)
            : base(session, linkSettings)
        {
            this.Exception = Preconditions.CheckNotNull(exception, nameof(exception));
        }

        public Exception Exception { get; }

        public override bool CreateDelivery(Transfer transfer, out Delivery delivery)
        {
            throw new NotImplementedException();
        }

        protected override void OnProcessTransfer(Delivery delivery, Transfer transfer, Frame rawFrame)
        {
            throw new NotImplementedException();
        }

        protected override void OnCreditAvailable(int session, uint link, bool drain, ArraySegment<byte> txnId)
        {
            throw new NotImplementedException();
        }

        protected override void OnDisposeDeliveryInternal(Delivery delivery)
        {
            throw new NotImplementedException();
        }
    }
}
