// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public interface IDeviceClient : IDisposable
    {
        Task OpenAsync(ConnectionStatusChangesHandler statusChangedHandler);

        Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback onDesiredPropertyChanged, object userContext);

        Task<Twin> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task SetMethodHandlerAsync(string methodName, MethodCallback callback, object userContext);
    }
}
