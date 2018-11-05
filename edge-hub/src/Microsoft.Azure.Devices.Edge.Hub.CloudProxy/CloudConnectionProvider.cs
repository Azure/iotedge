// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudConnectionProvider : ICloudConnectionProvider
    {
        // Minimum value allowed by the SDK for Connection Idle timeout for AMQP Multiplexed connections.
        static readonly TimeSpan MinAmqpConnectionMuxIdleTimeout = TimeSpan.FromSeconds(5);

        static readonly IDictionary<UpstreamProtocol, TransportType> UpstreamProtocolTransportTypeMap = new Dictionary<UpstreamProtocol, TransportType>
        {
            [UpstreamProtocol.Amqp] = TransportType.Amqp_Tcp_Only,
            [UpstreamProtocol.AmqpWs] = TransportType.Amqp_WebSocket_Only,
            [UpstreamProtocol.Mqtt] = TransportType.Mqtt_Tcp_Only,
            [UpstreamProtocol.MqttWs] = TransportType.Mqtt_WebSocket_Only
        };

        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly TimeSpan idleTimeout;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly bool closeOnIdleTimeout;
        Option<IEdgeHub> edgeHub;

        public CloudConnectionProvider(IMessageConverterProvider messageConverterProvider,
            int connectionPoolSize,
            IClientProvider clientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            ITokenProvider edgeHubTokenProvider,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout)
        {
            Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.transportSettings = GetTransportSettings(upstreamProtocol, connectionPoolSize);
            this.edgeHub = Option.None<IEdgeHub>();
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
        }

        public void BindEdgeHub(IEdgeHub edgeHubInstance)
        {
            this.edgeHub = Option.Some(Preconditions.CheckNotNull(edgeHubInstance, nameof(edgeHubInstance)));
        }

        internal static ITransportSettings[] GetTransportSettings(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize)
        {
            return upstreamProtocol
                .Map(
                    up =>
                    {
                        TransportType transportType = UpstreamProtocolTransportTypeMap[up];
                        switch (transportType)
                        {
                            case TransportType.Amqp_Tcp_Only:
                            case TransportType.Amqp_WebSocket_Only:
                                return new ITransportSettings[]
                                {
                                    new AmqpTransportSettings(transportType)
                                    {
                                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                                        {
                                            Pooling = true,
                                            MaxPoolSize = (uint)connectionPoolSize,
                                            ConnectionIdleTimeout = MinAmqpConnectionMuxIdleTimeout
                                        }
                                    }
                                };

                            case TransportType.Mqtt_Tcp_Only:
                            case TransportType.Mqtt_WebSocket_Only:
                                return new ITransportSettings[]
                                {
                                    new MqttTransportSettings(transportType)
                                };

                            default:
                                throw new ArgumentException($"Unsupported transport type {up}");
                        }
                    })
                .GetOrElse(
                    () => new ITransportSettings[] {
                            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                            {
                                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                                {
                                    Pooling = true,
                                    MaxPoolSize = (uint)connectionPoolSize,
                                    ConnectionIdleTimeout = MinAmqpConnectionMuxIdleTimeout
                                }
                            }
                        });
        }

        public async Task<Try<ICloudConnection>> Connect(IClientCredentials identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            try
            {
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Identity.Id);
                var cloudConnection = new CloudConnection(
                    connectionStatusChangedHandler,
                    this.transportSettings,
                    this.messageConverterProvider,
                    this.clientProvider,
                    cloudListener,
                    this.edgeHubTokenProvider,
                    this.deviceScopeIdentitiesCache,
                    this.idleTimeout,
                    this.closeOnIdleTimeout);

                await cloudConnection.CreateOrUpdateAsync(identity);
                Events.SuccessCreatingCloudConnection(identity.Identity);
                return Try.Success<ICloudConnection>(cloudConnection);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity.Identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();
            const int IdStart = CloudProxyEventIds.CloudConnectionProvider;

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnectSuccess
            }

            public static void SuccessCreatingCloudConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.CloudConnectSuccess, $"Created cloud connection for client {identity.Id}");
            }

            public static void ErrorCreatingCloudConnection(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.CloudConnectError, exception, $"Error creating cloud connection for client {identity.Id}");
            }
        }
    }
}
