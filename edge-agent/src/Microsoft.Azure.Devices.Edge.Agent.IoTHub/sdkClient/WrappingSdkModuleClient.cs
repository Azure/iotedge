// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class WrappingSdkModuleClient : ISdkModuleClient
    {
        readonly IotHubModuleClient sdkModuleClient;

        public WrappingSdkModuleClient(IotHubModuleClient sdkModuleClient)
            => this.sdkModuleClient = Preconditions.CheckNotNull(sdkModuleClient, nameof(sdkModuleClient));

        public Task OpenAsync()
        {
            try
            {
                return this.sdkModuleClient.OpenAsync();
            }
            catch (Exception)
            {
                if (this.sdkModuleClient != null)
                {
                    this.sdkModuleClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                throw;
            }
        }

        public void SetConnectionStatusChangesHandler(Action<ConnectionStatusInfo> statusChangesHandler)
            => this.sdkModuleClient.ConnectionStatusChangeCallback = statusChangesHandler;

        public Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyChanged)
            => this.sdkModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged);

        public Task SetMethodHandlerAsync(string methodName, Func<DirectMethodRequest, Task<DirectMethodResponse>> callback)
            => this.sdkModuleClient.SetDirectMethodCallbackAsync(callback);

        public Task SetDefaultMethodHandlerAsync(Func<DirectMethodRequest, Task<DirectMethodResponse>> callback)
            => this.sdkModuleClient.SetDirectMethodCallbackAsync(callback);

        public Task<TwinProperties> GetTwinAsync() => this.sdkModuleClient.GetTwinPropertiesAsync();

        public Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties)
            => this.sdkModuleClient.UpdateReportedPropertiesAsync(reportedProperties);

        //// public Task SendEventBatchAsync(IEnumerable<TelemetryMessage> messages) => this.sdkModuleClient.SendEventBatchAsync(messages);

        public Task SendEventAsync(TelemetryMessage message) => this.sdkModuleClient.SendTelemetryAsync(message);

        ////public Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken)
        ////    => this.sdkModuleClient.WaitForDeviceStreamRequestAsync(cancellationToken);

        ////public async Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken)
        ////{
        ////    await this.sdkModuleClient.AcceptDeviceStreamRequestAsync(deviceStreamRequest, cancellationToken);
        ////    return await EdgeClientWebSocket.Connect(deviceStreamRequest.Url, deviceStreamRequest.AuthorizationToken, cancellationToken);
        ////}

        public async Task CloseAsync()
        {
            await this.sdkModuleClient.DisposeAsync();
        }
    }
}
