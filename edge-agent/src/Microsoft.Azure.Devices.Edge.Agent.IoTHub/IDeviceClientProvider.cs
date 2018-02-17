// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface IDeviceClientProvider
    {
        Task<IDeviceClient> Create(ConnectionStatusChangesHandler statusChangedHandler, Func<IDeviceClient, Task> initialize);
    }
}
