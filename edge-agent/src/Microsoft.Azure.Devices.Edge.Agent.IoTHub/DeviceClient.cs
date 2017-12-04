// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class DeviceClient : IDeviceClient
    {
        Client.DeviceClient deviceClient = null;
        string moduleConnectionString = string.Empty;
        private const uint DeviceClientTimeout = 30000; // ms
      
        DeviceClient(string moduleConnectionString)
        {
            this.moduleConnectionString = moduleConnectionString;
        }

        public static DeviceClient Create(EdgeHubConnectionString deviceDetails)
        {
            Preconditions.CheckNotNull(deviceDetails, nameof(deviceDetails));
            Events.DeviceClientCreated();
            return new DeviceClient(ConstructModuleConnectionString(deviceDetails));
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

        public async Task OpenAsync(ConnectionStatusChangesHandler statusChangedHandler)
        {
            // The device SDK doesn't appear to be falling back to WebSocket from TCP,
            // so we'll do it explicitly until we can get the SDK sorted out.
            try
            {
                await Fallback.ExecuteAsync(
                    () => this.CreateAndOpenDeviceClient(TransportType.Amqp_Tcp_Only, statusChangedHandler),
                    () => this.CreateAndOpenDeviceClient(TransportType.Amqp_WebSocket_Only, statusChangedHandler));
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Events.DeviceConnectionError(ex);
                throw;
            }
        }

        async Task CreateAndOpenDeviceClient(TransportType transport, ConnectionStatusChangesHandler statusChangedHandler)
        {
            Events.AttemptingConnectionWithTransport(transport);
            this.deviceClient = Client.DeviceClient.CreateFromConnectionString(this.moduleConnectionString, transport);
            // note: it's important to set the status-changed handler and
            // timeout value *before* we open a connection to the hub
            this.deviceClient.OperationTimeoutInMilliseconds = DeviceClientTimeout;
            this.deviceClient.SetConnectionStatusChangesHandler(statusChangedHandler);
            await this.deviceClient.OpenAsync();
            Events.ConnectedWithTransport(transport);
        }

        public Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback onDesiredPropertyChanged, object userContext) =>
            this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, userContext);

        public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback, object userContext) => this.deviceClient.SetMethodHandlerAsync(methodName, callback, userContext);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceClient>();
            const int IdStart = AgentEventIds.DeviceClient;

            enum EventIds
            {
                AttemptingConnect = IdStart,
                Connected,
                DeviceClientCreated,
                DeviceConnectionError,
            }

            static string TransportName(TransportType type)
            {
                Preconditions.CheckArgument(type == TransportType.Amqp_Tcp_Only || type == TransportType.Amqp_WebSocket_Only);
                return type == TransportType.Amqp_Tcp_Only ? "AMQP" : "AMQP over WebSocket";
            }

            public static void AttemptingConnectionWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.AttemptingConnect, $"Edge agent attempting to connect to IoT Hub via {TransportName(transport)}...");
            }

            public static void ConnectedWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.Connected, $"Edge agent connected to IoT Hub via {TransportName(transport)}.");
            }

            public static void DeviceClientCreated()
            {
                Log.LogDebug((int)EventIds.DeviceClientCreated, "Device client for edge agent created.");
            }

            internal static void DeviceConnectionError(Exception ex)
            {
                Log.LogError((int)EventIds.DeviceConnectionError, ex, "Error creating a device to cloud connection");
            }
        }
    }
}
