// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class DeviceClient : IDeviceClient
    {
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new Util.TransientFaultHandling.ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        Client.DeviceClient deviceClient;
        string moduleConnectionString;
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

        public Task OpenAsync(
            ConnectionStatusChangesHandler statusChangedHandler,
            DesiredPropertyUpdateCallback onDesiredPropertyChanged,
            string methodName,
            MethodCallback callback)
        {
            return ExecuteWithRetry(
                    async () =>
                    {
                        // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                        // so we'll do it explicitly until we can get the SDK sorted out.
                        await Fallback.ExecuteAsync(
                            () => this.CreateAndOpenDeviceClient(TransportType.Amqp_Tcp_Only, statusChangedHandler),
                            () => this.CreateAndOpenDeviceClient(TransportType.Amqp_WebSocket_Only, statusChangedHandler),
                            Events.DeviceConnectionError);
                        await this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, null);
                        await this.deviceClient.SetMethodHandlerAsync(methodName, callback, null);
                    },
                    Events.RetryingDeviceClientConnection)
                .ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            Events.DeviceClientSetupFailed(t.Exception.InnerException);
                            System.Environment.Exit(1);
                        }
                    });
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

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            static readonly ISet<Type> NonTransientExceptions = new HashSet<Type>
            {
                typeof(ArgumentException),
                typeof(UnauthorizedException)
            };

            public bool IsTransient(Exception ex) => !(NonTransientExceptions.Contains(ex.GetType()));
        }

        public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

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
                RetryingDeviceClientConnection,
                DeviceClientSetupFailed
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

            public static void DeviceConnectionError(Exception ex)
            {
                Log.LogDebug((int)EventIds.DeviceConnectionError, ex, "Error creating a device-to-cloud connection");
            }

            public static void RetryingDeviceClientConnection(RetryingEventArgs args)
            {
                Log.LogDebug((int)EventIds.RetryingDeviceClientConnection,
                    $"Retrying connection to IoT Hub. Current retry count {args.CurrentRetryCount}.");
            }

            public static void DeviceClientSetupFailed(Exception ex)
            {
                Log.LogError((int)EventIds.DeviceClientSetupFailed, ex, "Device client threw non-transient exception during setup");
            }
        }
    }
}
