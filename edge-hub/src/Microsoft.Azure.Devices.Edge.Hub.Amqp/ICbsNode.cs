// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICbsNode : IAmqpAuthenticator, IDisposable
    {
        void RegisterLink(IAmqpLink link);

        Option<IIdentity> GetIdentity(string id);
    }
}
