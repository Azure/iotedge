// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    public class ModuleClientProvider : IModuleClientProvider
    {
        const uint ModuleClientTimeoutMilliseconds = 30000; // ms
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(60);

        readonly Option<string> connectionString;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<IWebProxy> proxy;
        readonly Option<string> productInfo;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan idleTimeout;
        readonly ISdkModuleClientProvider sdkModuleClientProvider;
        readonly bool useServerHeartbeat;

        public ModuleClientProvider(
            string connectionString,
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            Option<string> productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout,
            bool useServerHeartbeat)
            : this(Option.Maybe(connectionString), sdkModuleClientProvider, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout, useServerHeartbeat)
        {
        }

        public ModuleClientProvider(
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            Option<string> productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout,
            bool useServerHeartbeat)
            : this(Option.None<string>(), sdkModuleClientProvider, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout, useServerHeartbeat)
        {
        }

        ModuleClientProvider(
            Option<string> connectionString,
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            Option<string> productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout,
            bool useServerHeartbeat)
        {
            this.connectionString = connectionString;
            this.sdkModuleClientProvider = sdkModuleClientProvider;
            this.upstreamProtocol = upstreamProtocol;
            this.productInfo = productInfo;
            this.proxy = proxy;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.idleTimeout = idleTimeout;
            this.useServerHeartbeat = useServerHeartbeat;
        }

        public async Task<IModuleClient> Create(ConnectionStatusChangesHandler statusChangedHandler)
        {
            ISdkModuleClient sdkModuleClient = await this.CreateSdkModuleClientWithRetry(statusChangedHandler);
            IModuleClient moduleClient = new ModuleClient(sdkModuleClient, this.idleTimeout, this.closeOnIdleTimeout);
            return moduleClient;
        }

        static ITransportSettings GetTransportSettings(UpstreamProtocol protocol, Option<IWebProxy> proxy, bool useServerHeartbeat)
        {
            switch (protocol)
            {
                case UpstreamProtocol.Amqp: return CreateSettings(TransportType.Amqp_Tcp_Only);
                case UpstreamProtocol.AmqpWs: return CreateSettings(TransportType.Amqp_WebSocket_Only);
                case UpstreamProtocol.Mqtt: return CreateSettings(TransportType.Mqtt_Tcp_Only);
                case UpstreamProtocol.MqttWs: return CreateSettings(TransportType.Mqtt_WebSocket_Only);

                default:
                    {
                        throw new InvalidEnumArgumentException();
                    }
            }

            AmqpTransportSettings CreateSettings(TransportType transportType)
            {
                var settings = new AmqpTransportSettings(transportType);

                if (useServerHeartbeat)
                {
                    settings.IdleTimeout = HeartbeatTimeout;
                }

                proxy.ForEach(p => settings.Proxy = p);
                return settings;
            }
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        async Task<ISdkModuleClient> CreateSdkModuleClientWithRetry(ConnectionStatusChangesHandler statusChangedHandler)
        {
            try
            {
                ISdkModuleClient moduleClient = await ExecuteWithRetry(
                    () => this.CreateSdkModuleClient(statusChangedHandler),
                    Events.RetryingDeviceClientConnection);
                Events.DeviceClientCreated();
                return moduleClient;
            }
            catch (Exception e)
            {
                Events.DeviceClientSetupFailed(e);
                Environment.Exit(1);
                return null;
            }
        }

        Task<ISdkModuleClient> CreateSdkModuleClient(ConnectionStatusChangesHandler statusChangedHandler)
            => this.upstreamProtocol
                .Map(u => this.CreateAndOpenSdkModuleClient(u, statusChangedHandler))
                .GetOrElse(
                    async () =>
                    {
                        // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                        // so we'll do it explicitly until we can get the SDK sorted out.
                        Try<ISdkModuleClient> result = await Fallback.ExecuteAsync(
                            () => this.CreateAndOpenSdkModuleClient(UpstreamProtocol.Amqp, statusChangedHandler),
                            () => this.CreateAndOpenSdkModuleClient(UpstreamProtocol.AmqpWs, statusChangedHandler));

                        if (!result.Success)
                        {
                            Events.DeviceConnectionError(result.Exception);
                            ExceptionDispatchInfo.Capture(result.Exception).Throw();
                        }

                        return result.Value;
                    });

        async Task<ISdkModuleClient> CreateAndOpenSdkModuleClient(UpstreamProtocol upstreamProtocol, ConnectionStatusChangesHandler statusChangedHandler)
        {
            ITransportSettings settings = GetTransportSettings(upstreamProtocol, this.proxy, this.useServerHeartbeat);
            Events.AttemptingConnectionWithTransport(settings.GetTransportType());

            ISdkModuleClient moduleClient = await this.connectionString
                .Map(cs => Task.FromResult(this.sdkModuleClientProvider.GetSdkModuleClient(cs, settings)))
                .GetOrElse(this.sdkModuleClientProvider.GetSdkModuleClient(settings));

            this.productInfo.ForEach(p => moduleClient.SetProductInfo(p));

            // note: it's important to set the status-changed handler and
            // timeout value *before* we open a connection to the hub
            moduleClient.SetOperationTimeoutInMilliseconds(ModuleClientTimeoutMilliseconds);
            moduleClient.SetConnectionStatusChangesHandler(statusChangedHandler);
            await moduleClient.OpenAsync();

            Events.ConnectedWithTransport(settings.GetTransportType());
            return moduleClient;
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
            const int IdStart = AgentEventIds.ModuleClientProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClientProvider>();

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
