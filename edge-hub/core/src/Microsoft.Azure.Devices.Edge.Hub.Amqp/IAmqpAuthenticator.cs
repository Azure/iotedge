// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IAmqpAuthenticator
    {
        Task<bool> AuthenticateAsync(string id, Option<string> modelId);
    }
}
