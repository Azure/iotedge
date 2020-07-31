// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Threading.Tasks;

    public interface IAmqpAuthenticator
    {
        Task<bool> AuthenticateAsync(string id, Option<string> modelId);
    }
}
