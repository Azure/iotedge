// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface ISdkModuleClient
    {
        Task OpenAsync();

        void SetConnectionStatusChangesHandler(Action<ConnectionStatusInfo> statusChangesHandler);

        Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyChanged);

        Task SetMethodHandlerAsync(string methodName, Func<DirectMethodRequest, Task<DirectMethodResponse>> callback);

        Task SetDefaultMethodHandlerAsync(Func<DirectMethodRequest, Task<DirectMethodResponse>> callback);

        Task<TwinProperties> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties);

        //// Task SendEventBatchAsync(IEnumerable<TelemetryMessage> messages);

        Task SendEventAsync(TelemetryMessage message);

        ////Task<Client.DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken);

        ////Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(Client.DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken);

        Task CloseAsync();
    }
}
