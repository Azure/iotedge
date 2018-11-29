// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Threading.Tasks;

    interface IAmqpAuthenticator
    {
        Task<bool> AuthenticateAsync(string id);
    }
}
