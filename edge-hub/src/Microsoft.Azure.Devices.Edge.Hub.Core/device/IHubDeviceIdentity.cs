// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    public interface IHubDeviceIdentity
    {
        string Id { get; }
        string ConnectionString { get; }
    }
}