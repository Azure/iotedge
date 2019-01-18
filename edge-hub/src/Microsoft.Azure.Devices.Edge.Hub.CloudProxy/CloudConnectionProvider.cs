// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudConnectionProvider : ICloudConnectionProvider
    {
        // Minimum value allowed by the SDK for Connection Idle timeout for AMQP Multiplexed connections.
        static readonly TimeSpan MinAmqpConnectionMuxIdleTimeout = TimeSpan.FromSeconds(5);

        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly TimeSpan idleTimeout;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly bool closeOnIdleTimeout;
        readonly ICredentialsCache credentialsCache;
        readonly IIdentity edgeHubIdentity;
        readonly TimeSpan operationTimeout;
        Option<IEdgeHub> edgeHub;

        public CloudConnectionProvider(
            IMessageConverterProvider messageConverterProvider,
            int connectionPoolSize,
            IClientProvider clientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            ITokenProvider edgeHubTokenProvider,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            ICredentialsCache credentialsCache,
            IIdentity edgeHubIdentity,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            Option<IWebProxy> proxy)
        {
            Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.transportSettings = GetTransportSettings(upstreamProtocol, connectionPoolSize, proxy);
            this.edgeHub = Option.None<IEdgeHub>();
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.edgeHubIdentity = Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            this.operationTimeout = operationTimeout;
        }

        public void BindEdgeHub(IEdgeHub edgeHubInstance)
        {
            this.edgeHub = Option.Some(Preconditions.CheckNotNull(edgeHubInstance, nameof(edgeHubInstance)));
        }

        public async Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(clientCredentials, nameof(clientCredentials));

            try
            {
                Events.CreatingCloudConnectionUsingClientCredentials(clientCredentials);
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), clientCredentials.Identity.Id);

                if (this.edgeHubIdentity.Id.Equals(clientCredentials.Identity.Id))
                {
                    ICloudConnection cc = await CloudConnection.Create(
                        clientCredentials.Identity,
                        connectionStatusChangedHandler,
                        this.transportSettings,
                        this.messageConverterProvider,
                        this.clientProvider,
                        cloudListener,
                        this.edgeHubTokenProvider,
                        this.idleTimeout,
                        this.closeOnIdleTimeout,
                        this.operationTimeout);
                    Events.SuccessCreatingCloudConnection(clientCredentials.Identity);
                    return Try.Success(cc);
                }
                else if (clientCredentials is ITokenCredentials clientTokenCredentails)
                {
                    ICloudConnection cc = await ClientTokenCloudConnection.Create(
                        clientTokenCredentails,
                        connectionStatusChangedHandler,
                        this.transportSettings,
                        this.messageConverterProvider,
                        this.clientProvider,
                        cloudListener,
                        this.idleTimeout,
                        this.closeOnIdleTimeout,
                        this.operationTimeout);
                    Events.SuccessCreatingCloudConnection(clientCredentials.Identity);
                    return Try.Success(cc);
                }
                else
                {
                    throw new InvalidOperationException($"Cannot connect using client credentials of type {clientCredentials.AuthenticationType} for identity {clientCredentials.Identity.Id}");
                }
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(clientCredentials.Identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        public async Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            try
            {
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Id);
                Option<ServiceIdentity> serviceIdentity = (await this.deviceScopeIdentitiesCache.GetServiceIdentity(identity.Id))
                    .Filter(s => s.Status == ServiceIdentityStatus.Enabled);
                return await serviceIdentity
                    .Map(
                        async si =>
                        {
                            Events.CreatingCloudConnectionOnBehalfOf(identity);
                            ICloudConnection cc = await CloudConnection.Create(
                                identity,
                                connectionStatusChangedHandler,
                                this.transportSettings,
                                this.messageConverterProvider,
                                this.clientProvider,
                                cloudListener,
                                this.edgeHubTokenProvider,
                                this.idleTimeout,
                                this.closeOnIdleTimeout,
                                this.operationTimeout);
                            Events.SuccessCreatingCloudConnection(identity);
                            return Try.Success(cc);
                        })
                    .GetOrElse(
                        async () =>
                        {
                            Events.ServiceIdentityNotFound(identity);
                            Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(identity);
                            return await clientCredentials
                                .Map(cc => this.Connect(cc, connectionStatusChangedHandler))
                                .GetOrElse(() => throw new InvalidOperationException($"Unable to find identity {identity.Id} in device scopes cache or credentials cache"));
                        });
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        static ITransportSettings[] GetAmqpTransportSettings(TransportType type, int connectionPoolSize, Option<IWebProxy> proxy)
        {
            var settings = new AmqpTransportSettings(type)
            {
                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                {
                    Pooling = true,
                    MaxPoolSize = (uint)connectionPoolSize,
                    ConnectionIdleTimeout = MinAmqpConnectionMuxIdleTimeout
                }
            };
            proxy.ForEach(p => settings.Proxy = p);
            return new ITransportSettings[] { settings };
        }

        static ITransportSettings[] GetMqttTransportSettings(TransportType type, Option<IWebProxy> proxy)
        {
            var settings = new MqttTransportSettings(type);
            proxy.ForEach(p => settings.Proxy = p);
            return new ITransportSettings[] { settings };
        }

        internal static ITransportSettings[] GetTransportSettings(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, Option<IWebProxy> proxy)
        {
            return upstreamProtocol
                .Map(
                    up =>
                    {
                        switch (up)
                        {
                            case UpstreamProtocol.Amqp:
                                return GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, proxy);

                            case UpstreamProtocol.AmqpWs:
                                return GetAmqpTransportSettings(TransportType.Amqp_WebSocket_Only, connectionPoolSize, proxy);

                            case UpstreamProtocol.Mqtt:
                                return GetMqttTransportSettings(TransportType.Mqtt_Tcp_Only, proxy);

                            case UpstreamProtocol.MqttWs:
                                return GetMqttTransportSettings(TransportType.Mqtt_WebSocket_Only, proxy);

                            default:
                                throw new InvalidEnumArgumentException($"Unsupported transport type {up}");
                        }
                    })
                .GetOrElse(
                    () => GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, proxy));
        }

        static class Events
        {
            const int IdStart = CloudProxyEventIds.CloudConnectionProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();

            enum EventIds
            {
                CloudConnectError = IdStart,
                CloudConnectSuccess,
                CreatingCloudConnectionUsingClientCredentials,
                CreatingCloudConnectionOnBehalfOf,
                ServiceIdentityNotFound
            }

            public static void SuccessCreatingCloudConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.CloudConnectSuccess, $"Created cloud connection for client {identity.Id}");
            }

            public static void ErrorCreatingCloudConnection(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.CloudConnectError, exception, $"Error creating cloud connection for client {identity.Id}");
            }

            public static void CreatingCloudConnectionUsingClientCredentials(IClientCredentials clientCredentials)
            {
                Log.LogDebug((int)EventIds.CreatingCloudConnectionUsingClientCredentials, $"Creating cloud connection for client {clientCredentials.Identity.Id} using client credentials");
            }

            public static void CreatingCloudConnectionOnBehalfOf(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.CreatingCloudConnectionOnBehalfOf, $"Creating cloud connection for client {identity.Id} using EdgeHub credentials");
            }

            public static void ServiceIdentityNotFound(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityNotFound, $"Creating cloud connection for client {identity.Id}. Client identity is not in device scope, attempting to use client credentials.");
            }
        }
    }
}
