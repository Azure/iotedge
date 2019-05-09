// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class WrappingModuleClient : IModuleClient
    {
        readonly Client.ModuleClient moduleClient;

        public WrappingModuleClient(Client.ModuleClient moduleClient)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public event EventHandler Closed
        {
            add { }
            remove { }
        }

        public bool IsActive => true;

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged) =>
            this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, null);

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback) =>
            this.moduleClient.SetMethodHandlerAsync(methodName, callback, null);

        public Task SetDefaultMethodHandlerAsync(MethodCallback callback) =>
            this.moduleClient.SetMethodDefaultHandlerAsync(callback, null);

        public Task<Twin> GetTwinAsync() => this.moduleClient.GetTwinAsync();

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
            => this.moduleClient.UpdateReportedPropertiesAsync(reportedProperties);

        public Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken)
            => this.moduleClient.WaitForDeviceStreamRequestAsync(cancellationToken);

        public async Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken)
        {
            await this.moduleClient.AcceptDeviceStreamRequestAsync(deviceStreamRequest, cancellationToken);
            return await EdgeClientWebSocket.Connect(deviceStreamRequest.Url, deviceStreamRequest.AuthorizationToken, cancellationToken);
        }

        public Task CloseAsync() => this.moduleClient.CloseAsync();
    }
}
