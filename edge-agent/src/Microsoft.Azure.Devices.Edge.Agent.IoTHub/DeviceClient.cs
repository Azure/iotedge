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
        const uint DeviceClientTimeout = 30000; // ms
        static readonly IDictionary<UpstreamProtocol, TransportType> UpstreamProtocolTransportTypeMap = new Dictionary<UpstreamProtocol, TransportType>
        {
            [UpstreamProtocol.Amqp] = TransportType.Amqp_Tcp_Only,
            [UpstreamProtocol.AmqpWs] = TransportType.Amqp_WebSocket_Only,
            [UpstreamProtocol.Mqtt] = TransportType.Mqtt_Tcp_Only,
            [UpstreamProtocol.MqttWs] = TransportType.Mqtt_WebSocket_Only
        };

        readonly Client.DeviceClient deviceClient;

        DeviceClient(Client.DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }

        public static async Task<IDeviceClient> Create(string connectionString,
            Option<UpstreamProtocol> upstreamProtocol,
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<DeviceClient, Task> initialize)
        {
            try
            {
                return await ExecuteWithRetry(
                    async () =>
                    {
                        Client.DeviceClient dc = await CreateDeviceClientForUpstreamProtocol(upstreamProtocol, t => CreateAndOpenDeviceClient(t, connectionString, statusChangedHandler));
                        Events.DeviceClientCreated();
                        var deviceClient = new DeviceClient(dc);
                        if (initialize != null)
                        {
                            await initialize(deviceClient);
                        }
                        return deviceClient;
                    },
                    Events.RetryingDeviceClientConnection);
            }
            catch (Exception e)
            {
                Events.DeviceClientSetupFailed(e);
                Environment.Exit(1);
                return null;
            }
        }

        internal static Task<Client.DeviceClient> CreateDeviceClientForUpstreamProtocol(
            Option<UpstreamProtocol> upstreamProtocol,
            Func<TransportType, Task<Client.DeviceClient>> deviceClientCreator)
            => upstreamProtocol
            .Map(u => deviceClientCreator(UpstreamProtocolTransportTypeMap[u]))
            .GetOrElse(
                async () =>
                {
                    // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                    // so we'll do it explicitly until we can get the SDK sorted out.                        
                    Try<Client.DeviceClient> result = await Fallback.ExecuteAsync(
                        () => deviceClientCreator(TransportType.Amqp_Tcp_Only),
                        () => deviceClientCreator(TransportType.Amqp_WebSocket_Only));
                    if (!result.Success)
                    {
                        Events.DeviceConnectionError(result.Exception);
                        throw result.Exception;
                    }
                    return result.Value;
                });

        static async Task<Client.DeviceClient> CreateAndOpenDeviceClient(TransportType transport, string connectionString, ConnectionStatusChangesHandler statusChangedHandler)
        {
            Events.AttemptingConnectionWithTransport(transport);
            Client.DeviceClient deviceClient = Client.DeviceClient.CreateFromConnectionString(connectionString, transport);
            // note: it's important to set the status-changed handler and
            // timeout value *before* we open a connection to the hub
            deviceClient.OperationTimeoutInMilliseconds = DeviceClientTimeout;
            deviceClient.SetConnectionStatusChangesHandler(statusChangedHandler);
            await deviceClient.OpenAsync();
            Events.ConnectedWithTransport(transport);
            return deviceClient;
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry)
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

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged) =>
            this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, null);

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback) =>
            this.deviceClient.SetMethodHandlerAsync(methodName, callback, null);

        public void Dispose() => this.deviceClient.Dispose();

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
