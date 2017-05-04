// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    public interface IIdentity
    {
        string Id { get; }

        string ConnectionString { get; }
    }
}