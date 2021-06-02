// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface ITokenCredentials : IClientCredentials
    {
        string Token { get; }

        bool IsUpdatable { get; }
    }
}
