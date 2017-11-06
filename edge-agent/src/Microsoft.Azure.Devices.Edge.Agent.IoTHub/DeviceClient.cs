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
      
        DeviceClient(Client.DeviceClient deviceClient)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
        }

        public static DeviceClient Create(EdgeHubConnectionString deviceDetails)
        {
            Preconditions.CheckNotNull(deviceDetails, nameof(deviceDetails));
            
            string moduleString = ConstructModuleConnectionString(deviceDetails);

            Client.DeviceClient deviceClient = Client.DeviceClient.CreateFromConnectionString(moduleString);
            deviceClient.OperationTimeoutInMilliseconds = DeviceClientTimeout;

            Events.DeviceClientCreated();
            return new DeviceClient(deviceClient);
        }

        static string ConstructModuleConnectionString(EdgeHubConnectionString connectionDetails)
        {
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
        

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceClient>();
            const int IdStart = AgentEventIds.DeviceClient;

            enum EventIds
            {
                DeviceClientCreated = IdStart
            }

            public static void DeviceClientCreated()
            {
                Log.LogDebug((int)EventIds.DeviceClientCreated, "Device Client for Agent Module Created.");
            }            
        }
    }
}
