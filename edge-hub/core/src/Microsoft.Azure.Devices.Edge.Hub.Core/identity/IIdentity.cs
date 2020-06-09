// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IIdentity
    {
        string Id { get; }

        string IotHubHostName { get; }
    }
}
