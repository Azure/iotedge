// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface IModuleClientProvider
    {
        Task<IModuleClient> Create(ConnectionStatusChangesHandler statusChangedHandler, Func<IModuleClient, Task> initialize);
    }
}
