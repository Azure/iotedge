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

    public class ModuleClient : IModuleClient
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

        readonly Client.ModuleClient deviceClient;

        ModuleClient(Client.ModuleClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }

        public static Task<IModuleClient> Create(Option<string> connectionString,
            Option<UpstreamProtocol> upstreamProtocol,
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<ModuleClient, Task> initialize,
            Option<string> productInfo) =>
            Create(upstreamProtocol, initialize, t => CreateAndOpenDeviceClient(t, connectionString, statusChangedHandler, productInfo));

        static async Task<IModuleClient> Create(Option<UpstreamProtocol> upstreamProtocol, Func<ModuleClient, Task> initialize, Func<TransportType, Task<Client.ModuleClient>> deviceClientCreator)
        {
            try
            {
                return await ExecuteWithRetry(
                    async () =>
                    {
                        Client.ModuleClient dc = await CreateDeviceClientForUpstreamProtocol(upstreamProtocol, deviceClientCreator);
                        Events.DeviceClientCreated();
                        var deviceClient = new ModuleClient(dc);
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

        internal static Task<Client.ModuleClient> CreateDeviceClientForUpstreamProtocol(
            Option<UpstreamProtocol> upstreamProtocol,
            Func<TransportType, Task<Client.ModuleClient>> deviceClientCreator)
            => upstreamProtocol
            .Map(u => deviceClientCreator(UpstreamProtocolTransportTypeMap[u]))
            .GetOrElse(
                async () =>
                {
                    // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                    // so we'll do it explicitly until we can get the SDK sorted out.
                    Try<Client.ModuleClient> result = await Fallback.ExecuteAsync(
                        () => deviceClientCreator(TransportType.Amqp_Tcp_Only),
                        () => deviceClientCreator(TransportType.Amqp_WebSocket_Only));
                    if (!result.Success)
                    {
                        Events.DeviceConnectionError(result.Exception);
                        throw result.Exception;
                    }
                    return result.Value;
                });

        static async Task<Client.ModuleClient> CreateAndOpenDeviceClient(TransportType transport,
            Option<string> connectionString,
            ConnectionStatusChangesHandler statusChangedHandler,
            Option<string> productInfo)
        {
            Events.AttemptingConnectionWithTransport(transport);
            Client.ModuleClient deviceClient = await connectionString.Map(cs => Task.FromResult(Client.ModuleClient.CreateFromConnectionString(cs, transport)))
                .GetOrElse(() => Client.ModuleClient.CreateFromEnvironmentAsync(transport));
            productInfo.ForEach(p => deviceClient.ProductInfo = p);
            await OpenAsync(statusChangedHandler, deviceClient);
            Events.ConnectedWithTransport(transport);
            return deviceClient;
        }

        static async Task OpenAsync(ConnectionStatusChangesHandler statusChangedHandler, Client.ModuleClient deviceClient)
        {
            // note: it's important to set the status-changed handler and
            // timeout value *before* we open a connection to the hub
            deviceClient.OperationTimeoutInMilliseconds = DeviceClientTimeout;
            deviceClient.SetConnectionStatusChangesHandler(statusChangedHandler);
            await deviceClient.OpenAsync();
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();
            const int IdStart = AgentEventIds.ModuleClient;

            enum EventIds
            {
                AttemptingConnect = IdStart,
                Connected,
                DeviceClientCreated,
                DeviceConnectionError,
                RetryingDeviceClientConnection,
                DeviceClientSetupFailed
            }

            public static void AttemptingConnectionWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.AttemptingConnect, $"Edge agent attempting to connect to IoT Hub via {transport.ToString()}...");
            }

            public static void ConnectedWithTransport(TransportType transport)
            {
                Log.LogInformation((int)EventIds.Connected, $"Edge agent connected to IoT Hub via {transport.ToString()}.");
            }

            public static void DeviceClientCreated()
            {
                Log.LogDebug((int)EventIds.DeviceClientCreated, "Device client for edge agent created.");
            }

            public static void DeviceConnectionError(Exception ex)
            {
                Log.LogWarning((int)EventIds.DeviceConnectionError, ex, "Error creating a device-to-cloud connection");
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
