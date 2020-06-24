// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    /// <summary>
    /// This interface contains functionality similar to AmqpSession.
    /// This allows unit testing the components that use it
    /// </summary>
    public interface IAmqpSession
    {
        IAmqpConnection Connection { get; }
    }
}
