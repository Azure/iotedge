// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    public class ModuleClient : IModuleClient
    {
        const uint DeviceClientTimeout = 30000; // ms
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        readonly Client.ModuleClient moduleClient;

        ModuleClient(Client.ModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
        }

        public static Task<IModuleClient> Create(
            Option<string> connectionString,
            Option<UpstreamProtocol> upstreamProtocol,
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<ModuleClient, Task> initialize,
            Option<IWebProxy> proxy,
            Option<string> productInfo) =>
            Create(upstreamProtocol, initialize, u => CreateAndOpenDeviceClient(u, connectionString, statusChangedHandler, proxy, productInfo));

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged) =>
            this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged, null);

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback) =>
            this.moduleClient.SetMethodHandlerAsync(methodName, callback, null);

        public Task SetDefaultMethodHandlerAsync(MethodCallback callback) =>
            this.moduleClient.SetMethodDefaultHandlerAsync(callback, null);

        public void Dispose() => this.moduleClient.Dispose();

        public Task<Twin> GetTwinAsync() => this.moduleClient.GetTwinAsync();

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.moduleClient.UpdateReportedPropertiesAsync(reportedProperties);

        internal static Task<Client.ModuleClient> CreateDeviceClientForUpstreamProtocol(
            Option<UpstreamProtocol> upstreamProtocol,
            Func<UpstreamProtocol, Task<Client.ModuleClient>> deviceClientCreator)
            => upstreamProtocol
                .Map(deviceClientCreator)
                .GetOrElse(
                    async () =>
                    {
                        // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                        // so we'll do it explicitly until we can get the SDK sorted out.
                        Try<Client.ModuleClient> result = await Fallback.ExecuteAsync(
                            () => deviceClientCreator(UpstreamProtocol.Amqp),
                            () => deviceClientCreator(UpstreamProtocol.AmqpWs));
                        if (!result.Success)
                        {
                            Events.DeviceConnectionError(result.Exception);
                            throw result.Exception;
                        }

                        return result.Value;
                    });

        static async Task<IModuleClient> Create(Option<UpstreamProtocol> upstreamProtocol, Func<ModuleClient, Task> initialize, Func<UpstreamProtocol, Task<Client.ModuleClient>> deviceClientCreator)
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

        static ITransportSettings GetTransportSettings(UpstreamProtocol protocol, Option<IWebProxy> proxy)
        {
            switch (protocol)
            {
                case UpstreamProtocol.Amqp:
                {
                    var settings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                    proxy.ForEach(p => settings.Proxy = p);
                    return settings;
                }

                case UpstreamProtocol.AmqpWs:
                {
                    var settings = new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only);
                    proxy.ForEach(p => settings.Proxy = p);
                    return settings;
                }

                case UpstreamProtocol.Mqtt:
                {
                    var settings = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                    proxy.ForEach(p => settings.Proxy = p);
                    return settings;
                }

                case UpstreamProtocol.MqttWs:
                {
                    var settings = new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only);
                    proxy.ForEach(p => settings.Proxy = p);
                    return settings;
                }

                default:
                {
                    throw new InvalidEnumArgumentException();
                }
            }
        }

        static async Task<Client.ModuleClient> CreateAndOpenDeviceClient(
            UpstreamProtocol upstreamProtocol,
            Option<string> connectionString,
            ConnectionStatusChangesHandler statusChangedHandler,
            Option<IWebProxy> proxy,
            Option<string> productInfo)
        {
            ITransportSettings settings = GetTransportSettings(upstreamProtocol, proxy);
            Events.AttemptingConnectionWithTransport(settings.GetTransportType());
            Client.ModuleClient deviceClient = await connectionString
                .Map(cs => Task.FromResult(Client.ModuleClient.CreateFromConnectionString(cs, new[] { settings })))
                .GetOrElse(() => Client.ModuleClient.CreateFromEnvironmentAsync(new[] { settings }));
            productInfo.ForEach(p => deviceClient.ProductInfo = p);
            await OpenAsync(statusChangedHandler, deviceClient);
            Events.ConnectedWithTransport(settings.GetTransportType());
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

            public bool IsTransient(Exception ex) => !NonTransientExceptions.Contains(ex.GetType());
        }

        static class Events
        {
            const int IdStart = AgentEventIds.ModuleClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();

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
                Log.LogDebug(
                    (int)EventIds.RetryingDeviceClientConnection,
                    $"Retrying connection to IoT Hub. Current retry count {args.CurrentRetryCount}.");
            }

            public static void DeviceClientSetupFailed(Exception ex)
            {
                Log.LogError((int)EventIds.DeviceClientSetupFailed, ex, "Device client threw non-transient exception during setup");
            }
        }
    }
}
