// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IIdentity
    {
        string Id { get; }

        string ConnectionString { get; }

        string ProductInfo { get; }
    }
}
