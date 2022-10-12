// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class WrappingSdkModuleClient : ISdkModuleClient
    {
        readonly ModuleClient sdkModuleClient;
        readonly object getTwinTaskLock = new object();
        readonly TimeSpan cloudConnectionHangingTimeout;
        Task<Twin> getTwinTask;
        static Action sdkTimeoutAction = () => throw new EdgeAgentCloudSDKException("Operation timed out due to SDK hanging");

        public WrappingSdkModuleClient(ModuleClient sdkModuleClient, TimeSpan cloudConnectionHangingTimeout)
        {
            this.sdkModuleClient = Preconditions.CheckNotNull(sdkModuleClient, nameof(sdkModuleClient));
            this.cloudConnectionHangingTimeout = cloudConnectionHangingTimeout;
        }

        public Task OpenAsync()
        {
            try
            {
                return this.sdkModuleClient.OpenAsync().TimeoutAfter(this.cloudConnectionHangingTimeout, sdkTimeoutAction);
            }
            catch (Exception)
            {
                this.sdkModuleClient?.Dispose();
                throw;
            }
        }

        public void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler)
            => this.sdkModuleClient.SetConnectionStatusChangesHandler(statusChangesHandler);

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutInMilliseconds)
            => this.sdkModuleClient.OperationTimeoutInMilliseconds = operationTimeoutInMilliseconds;

        public void SetProductInfo(string productInfo) => this.sdkModuleClient.ProductInfo = productInfo;

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged)
            => this.sdkModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, null);

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback)
            => this.sdkModuleClient.SetMethodHandlerAsync(methodName, callback, null);

        public Task SetDefaultMethodHandlerAsync(MethodCallback callback)
            => this.sdkModuleClient.SetMethodDefaultHandlerAsync(callback, null);

        public Task<Twin> GetTwinAsync()
        {
            // limit get twin call to single one to avoid redundant network flow
            lock (this.getTwinTaskLock)
            {
                if (this.getTwinTask?.Status != TaskStatus.Running)
                {
                    this.getTwinTask = this.sdkModuleClient.GetTwinAsync();
                }
            }

            return this.getTwinTask;
        }

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
            => this.sdkModuleClient.UpdateReportedPropertiesAsync(reportedProperties);

        //// public Task SendEventBatchAsync(IEnumerable<Message> messages) => this.sdkModuleClient.SendEventBatchAsync(messages);

        public Task SendEventAsync(Message message) => this.sdkModuleClient.SendEventAsync(message);

        ////public Task<DeviceStreamRequest> WaitForDeviceStreamRequestAsync(CancellationToken cancellationToken)
        ////    => this.sdkModuleClient.WaitForDeviceStreamRequestAsync(cancellationToken);

        ////public async Task<IClientWebSocket> AcceptDeviceStreamingRequestAndConnect(DeviceStreamRequest deviceStreamRequest, CancellationToken cancellationToken)
        ////{
        ////    await this.sdkModuleClient.AcceptDeviceStreamRequestAsync(deviceStreamRequest, cancellationToken);
        ////    return await EdgeClientWebSocket.Connect(deviceStreamRequest.Url, deviceStreamRequest.AuthorizationToken, cancellationToken);
        ////}

        public Task CloseAsync()
        {
            this.sdkModuleClient.Dispose();
            return Task.CompletedTask;
        }
    }
}
