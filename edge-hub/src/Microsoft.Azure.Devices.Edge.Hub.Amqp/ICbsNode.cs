// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;

    public interface ICbsNode : IAmqpAuthenticator, IDisposable
    {
        void RegisterLink(IAmqpLink link);
    }
}
