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

        readonly Option<string> connectionString;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<IWebProxy> proxy;
        readonly string productInfo;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan idleTimeout;
        readonly ISdkModuleClientProvider sdkModuleClientProvider;

        public ModuleClientProvider(
            string connectionString,
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            string productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout)
            : this(Option.Maybe(connectionString), sdkModuleClientProvider, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout)
        {
        }

        public ModuleClientProvider(
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            string productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout)
            : this(Option.None<string>(), sdkModuleClientProvider, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout)
        {
        }

        ModuleClientProvider(
            Option<string> connectionString,
            ISdkModuleClientProvider sdkModuleClientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> proxy,
            string productInfo,
            bool closeOnIdleTimeout,
            TimeSpan idleTimeout)
        {
            this.connectionString = connectionString;
            this.sdkModuleClientProvider = sdkModuleClientProvider;
            this.upstreamProtocol = upstreamProtocol;
            this.productInfo = Preconditions.CheckNotNull(productInfo, nameof(productInfo));
            this.proxy = proxy;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.idleTimeout = idleTimeout;
        }

        public async Task<IModuleClient> Create(ConnectionStatusChangesHandler statusChangedHandler)
        {
            (ISdkModuleClient sdkModuleClient, UpstreamProtocol protocol) = await this.CreateSdkModuleClientWithRetry(statusChangedHandler);
            IModuleClient moduleClient = new ModuleClient(sdkModuleClient, this.idleTimeout, this.closeOnIdleTimeout, protocol);
            return moduleClient;
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

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        async Task<(ISdkModuleClient sdkModuleClient, UpstreamProtocol upstreamProtocol)> CreateSdkModuleClientWithRetry(ConnectionStatusChangesHandler statusChangedHandler)
        {
            try
            {
                (ISdkModuleClient moduleClient, UpstreamProtocol protocol) = await ExecuteWithRetry(
                    () => this.CreateSdkModuleClient(statusChangedHandler),
                    Events.RetryingDeviceClientConnection);
                Events.DeviceClientCreated();
                return (moduleClient, protocol);
            }
            catch (Exception e)
            {
                Events.DeviceClientSetupFailed(e);
                Environment.Exit(1);
                return (null, UpstreamProtocol.Amqp);
            }
        }

        Task<(ISdkModuleClient sdkModuleClient, UpstreamProtocol upstreamProtocol)> CreateSdkModuleClient(ConnectionStatusChangesHandler statusChangedHandler)
            => this.upstreamProtocol
                .Map(async u =>
                {
                    ISdkModuleClient sdkModuleClient = await this.CreateAndOpenSdkModuleClient(u, statusChangedHandler);
                    return (sdkModuleClient, u);
                })
                .GetOrElse(
                    async () =>
                    {
                        // The device SDK doesn't appear to be falling back to WebSocket from TCP,
                        // so we'll do it explicitly until we can get the SDK sorted out.
                        UpstreamProtocol protocol;
                        Try<(ISdkModuleClient, UpstreamProtocol)> result = await Fallback.ExecuteAsync(
                            async () =>
                            {
                                protocol = UpstreamProtocol.Amqp;
                                ISdkModuleClient sdkModuleClient = await this.CreateAndOpenSdkModuleClient(protocol, statusChangedHandler);
                                return (sdkModuleClient, protocol);
                            },
                            async () =>
                            {
                                protocol = UpstreamProtocol.AmqpWs;
                                ISdkModuleClient sdkModuleClient = await this.CreateAndOpenSdkModuleClient(protocol, statusChangedHandler);
                                return (sdkModuleClient, protocol);
                            });

                        if (!result.Success)
                        {
                            Events.DeviceConnectionError(result.Exception);
                            ExceptionDispatchInfo.Capture(result.Exception).Throw();
                        }

                        return result.Value;
                    });

        async Task<ISdkModuleClient> CreateAndOpenSdkModuleClient(UpstreamProtocol upstreamProtocol, ConnectionStatusChangesHandler statusChangedHandler)
        {
            ITransportSettings settings = GetTransportSettings(upstreamProtocol, this.proxy);
            Events.AttemptingConnectionWithTransport(settings.GetTransportType());

            ISdkModuleClient moduleClient = await this.connectionString
                .Map(cs => Task.FromResult(this.sdkModuleClientProvider.GetSdkModuleClient(cs, settings)))
                .GetOrElse(this.sdkModuleClientProvider.GetSdkModuleClient(settings));

            moduleClient.SetProductInfo(this.productInfo);

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
