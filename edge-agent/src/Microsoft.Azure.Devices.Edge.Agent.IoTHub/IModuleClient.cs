// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IModuleClient
    {
        event EventHandler Closed;

        bool IsActive { get; }

        UpstreamProtocol UpstreamProtocol { get; }

        Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyChanged);

        Task SetMethodHandlerAsync(string methodName, Func<DirectMethodRequest, Task<DirectMethodResponse>> callback);

        Task SetDefaultMethodHandlerAsync(Func<DirectMethodRequest, Task<DirectMethodResponse>> callback);

        Task<TwinProperties> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties);

        //// Task SendEventBatchAsync(IEnumerable<TelemetryMessage> messages);

        Task SendEventAsync(TelemetryMessage message);

        ////Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken);

        ////Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken);

        Task CloseAsync();
    }
}
