// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;

    public interface ICbsNode : IDisposable
    {
        void RegisterLink(IAmqpLink link);

        Task<AmqpAuthentication> GetAmqpAuthentication();
    }
}
