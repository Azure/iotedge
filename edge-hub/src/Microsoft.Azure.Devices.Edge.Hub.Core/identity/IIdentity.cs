// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IIdentity
    {
        string Id { get; }

        string IotHubHostname { get; }
    }
}
