// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;

    interface ICbsNode : IAmqpAuthenticator, IDisposable
    {
        void RegisterLink(IAmqpLink link);

        Task<AmqpAuthentication> GetAmqpAuthentication();
    }
}
