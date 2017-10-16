// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class DeviceClient : IDeviceClient
    {
        readonly Client.DeviceClient deviceClient;
        private const uint DeviceClientTimeout = 30000; // ms
        static readonly ITransientErrorDetectionStrategy TransientDetectionStrategy = new DeviceClientRetryStrategy();
        static readonly RetryStrategy TransientRetryStrategy = new ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        DeviceClient(Client.DeviceClient deviceClient)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
        }

        public static async Task<DeviceClient> CreateAsync(EdgeHubConnectionString deviceDetails, IServiceClient deviceAuthorizedServiceClient)
        {
            Preconditions.CheckNotNull(deviceDetails, nameof(deviceDetails));
            Preconditions.CheckNotNull(deviceAuthorizedServiceClient, nameof(deviceAuthorizedServiceClient));

            string moduleString = await ConstructModuleConnectionStringAsync(deviceDetails, deviceAuthorizedServiceClient);

            Client.DeviceClient deviceClient = Client.DeviceClient.CreateFromConnectionString(moduleString);
            deviceClient.OperationTimeoutInMilliseconds = DeviceClientTimeout;

            Events.DeviceClientCreated();
            return new DeviceClient(deviceClient);
        }

        static async Task<string> ConstructModuleConnectionStringAsync(EdgeHubConnectionString connectionDetails, IServiceClient deviceAuthorizedServiceClient)
        {
            var transientRetryPolicy = new RetryPolicy(TransientDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => Events.GetModuleFailed(args);
            // ReSharper disable once UnusedVariable
            Module agentModule = await transientRetryPolicy.ExecuteAsync(() => deviceAuthorizedServiceClient.GetModule(Constants.EdgeAgentModuleIdentityName));

            // TODO: should be using agentModule's authentication
            EdgeHubConnectionString agentConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(connectionDetails.HostName, connectionDetails.DeviceId)
                .SetSharedAccessKey(connectionDetails.SharedAccessKey)
                .SetModuleId(Constants.EdgeAgentModuleIdentityName)
                .Build();
            return agentConnectionString.ToConnectionString();
        }

        public void Dispose() => this.deviceClient.Dispose();

        public Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback onDesiredPropertyChanged, object userContext) =>
            this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, userContext);

        public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler statusChangedHandler) =>
            this.deviceClient.SetConnectionStatusChangesHandler(statusChangedHandler);

        class DeviceClientRetryStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => !(ex is ArgumentException);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Agent>();
            const int IdStart = AgentEventIds.DeviceClient;

            enum EventIds
            {
                DeviceClientCreated = IdStart,
                GetModuleFailed,
            }

            public static void DeviceClientCreated()
            {
                Log.LogDebug((int)EventIds.DeviceClientCreated, "Device Client for Agent Module Created.");
            }

            public static void GetModuleFailed(RetryingEventArgs args)
            {
                Log.LogWarning((int)EventIds.GetModuleFailed, args.LastException, "Attempt to get Agent Module from service failed.");
            }
        }
    }
}
