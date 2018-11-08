// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
