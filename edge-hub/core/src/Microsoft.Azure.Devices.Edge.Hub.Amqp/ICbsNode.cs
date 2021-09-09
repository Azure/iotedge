// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;

    public interface ICbsNode : IDisposable
    {
        void RegisterLink(IAmqpLink link);
    }
}
