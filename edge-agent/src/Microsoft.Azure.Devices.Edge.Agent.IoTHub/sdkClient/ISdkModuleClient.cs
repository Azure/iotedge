// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public interface ISdkModuleClient
    {
        Task OpenAsync();

        void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler);

        void SetOperationTimeoutInMilliseconds(uint operationTimeoutInMilliseconds);

        void SetProductInfo(string productInfo);

        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged);

        Task SetMethodHandlerAsync(string methodName, MethodCallback callback);

        Task SetDefaultMethodHandlerAsync(MethodCallback callback);

        Task<Twin> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task SendEventAsync(Message message);

        ////Task<Client.DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken);

        ////Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(Client.DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken);

        Task CloseAsync();
    }
}
