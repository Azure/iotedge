// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;

    public interface ICbsNode : IDisposable
    {
        Task<AmqpAuthentication> GetAmqpAuthentication();

        void RegisterLink(IAmqpLink link);
    }
}
