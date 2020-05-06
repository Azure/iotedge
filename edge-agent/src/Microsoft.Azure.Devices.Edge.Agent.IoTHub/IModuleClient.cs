// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Shared;

    public interface IModuleClient
    {
        event EventHandler Closed;

        bool IsActive { get; }

        UpstreamProtocol UpstreamProtocol { get; }

        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged);

        Task SetMethodHandlerAsync(string methodName, MethodCallback callback);

        Task SetDefaultMethodHandlerAsync(MethodCallback callback);

        Task<Twin> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task SendEventAsync(Message message);

        ////Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken);

        ////Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken);

        Task CloseAsync();
    }
}
