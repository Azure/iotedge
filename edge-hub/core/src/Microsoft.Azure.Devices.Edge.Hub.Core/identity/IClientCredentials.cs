// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IClientCredentials
    {
        IIdentity Identity { get; }

        AuthenticationType AuthenticationType { get; }

        string ProductInfo { get; }
    }
}
