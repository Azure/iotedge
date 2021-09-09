// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudConnectionProvider : ICloudConnectionProvider
    {
        static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(60);

        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly int connectionPoolSize;
        readonly Option<IWebProxy> proxy;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly TimeSpan idleTimeout;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly bool closeOnIdleTimeout;
        readonly bool useServerHeartbeat;
        readonly ICredentialsCache credentialsCache;
        readonly IIdentity edgeHubIdentity;
        readonly TimeSpan operationTimeout;
        readonly IMetadataStore metadataStore;
        readonly bool nestedEdgeEnabled;
        readonly bool scopeAuthenticationOnly;
        readonly bool trackDeviceState;

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
            bool useServerHeartbeat,
            Option<IWebProxy> proxy,
            IMetadataStore metadataStore,
            bool scopeAuthenticationOnly,
            bool trackDeviceState,
            bool nestedEdgeEnabled = true)
        {
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.upstreamProtocol = upstreamProtocol;
            this.connectionPoolSize = Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.proxy = proxy;
            this.edgeHub = Option.None<IEdgeHub>();
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.useServerHeartbeat = useServerHeartbeat;
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.edgeHubIdentity = Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            this.operationTimeout = operationTimeout;
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));
            this.nestedEdgeEnabled = nestedEdgeEnabled;
            this.scopeAuthenticationOnly = scopeAuthenticationOnly;
            this.trackDeviceState = trackDeviceState;
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
                ConnectionMetadata connectionMetadata = await this.metadataStore.GetMetadata(clientCredentials.Identity.Id);
                string productInfo = connectionMetadata.EdgeProductInfo;
                Option<string> modelId = clientCredentials.ModelId.HasValue ? clientCredentials.ModelId : connectionMetadata.ModelId;
                string authChain = string.Empty;
                if (this.nestedEdgeEnabled)
                {
                    Option<string> authChainMaybe = await this.deviceScopeIdentitiesCache.GetAuthChain(clientCredentials.Identity.Id);

                    // It's possible to have no auth-chain for out-of-scope leaf devices connecting through
                    // us as a gateway. In this case we let the upstream connection happen anyways, as any
                    // unauthorized attempt here would be denied by IoTHub.
                    authChain = authChainMaybe.OrDefault();
                }

                // Get the transport settings
                ITransportSettings[] transportSettings = GetTransportSettings(
                    this.upstreamProtocol,
                    this.connectionPoolSize,
                    this.proxy,
                    this.useServerHeartbeat,
                    authChain);

                if (this.edgeHubIdentity.Id.Equals(clientCredentials.Identity.Id))
                {
                    ICloudConnection cc = await CloudConnection.Create(
                        clientCredentials.Identity,
                        connectionStatusChangedHandler,
                        transportSettings,
                        this.messageConverterProvider,
                        this.clientProvider,
                        cloudListener,
                        this.edgeHubTokenProvider,
                        this.idleTimeout,
                        this.closeOnIdleTimeout,
                        this.operationTimeout,
                        productInfo,
                        modelId);
                    Events.SuccessCreatingCloudConnection(clientCredentials.Identity);
                    return Try.Success(cc);
                }
                else if (clientCredentials is ITokenCredentials clientTokenCredentails)
                {
                    ICloudConnection cc = await ClientTokenCloudConnection.Create(
                        clientTokenCredentails,
                        connectionStatusChangedHandler,
                        transportSettings,
                        this.messageConverterProvider,
                        this.clientProvider,
                        cloudListener,
                        this.idleTimeout,
                        this.closeOnIdleTimeout,
                        this.operationTimeout,
                        productInfo,
                        modelId);
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

        public Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler) =>
            this.trackDeviceState
            ? this.ConnectInternalWithDeviceStateTracking(identity, connectionStatusChangedHandler, false)
            : this.ConnectInternal(identity, connectionStatusChangedHandler);

        async Task<Try<ICloudConnection>> ConnectInternal(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            try
            {
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Id);
                Option<ServiceIdentity> serviceIdentity = (await this.deviceScopeIdentitiesCache.GetServiceIdentity(identity.Id))
                    .Filter(s => s.Status == ServiceIdentityStatus.Enabled);

                string authChain = string.Empty;
                if (this.nestedEdgeEnabled)
                {
                    Option<string> authChainMaybe = await this.deviceScopeIdentitiesCache.GetAuthChain(identity.Id);
                    authChain = authChainMaybe.Expect(() => new InvalidOperationException($"No auth chain for the client identity: {identity.Id}"));
                }

                ITransportSettings[] transportSettings = GetTransportSettings(
                    this.upstreamProtocol,
                    this.connectionPoolSize,
                    this.proxy,
                    this.useServerHeartbeat,
                    authChain);

                return await serviceIdentity
                    .Map(
                        async si =>
                        {
                            Events.CreatingCloudConnectionOnBehalfOf(identity);
                            ConnectionMetadata connectionMetadata = await this.metadataStore.GetMetadata(identity.Id);
                            string productInfo = connectionMetadata.EdgeProductInfo;
                            Option<string> modelId = connectionMetadata.ModelId;
                            ICloudConnection cc = await CloudConnection.Create(
                                identity,
                                connectionStatusChangedHandler,
                                transportSettings,
                                this.messageConverterProvider,
                                this.clientProvider,
                                cloudListener,
                                this.edgeHubTokenProvider,
                                this.idleTimeout,
                                this.closeOnIdleTimeout,
                                this.operationTimeout,
                                productInfo,
                                modelId);
                            Events.SuccessCreatingCloudConnection(identity);
                            return Try.Success(cc);
                        })
                    .GetOrElse(
                        async () =>
                        {
                            // allow to use credential cache when auth mode is not Scope only (could be CloudAndScope or Cloud) or identity is for edgeHub
                            if (!this.scopeAuthenticationOnly || this.edgeHubIdentity.Id.Equals(identity.Id))
                            {
                                Events.ServiceIdentityNotFound(identity);
                                Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(identity);
                                var clientCredential = clientCredentials.Expect(() => new InvalidOperationException($"Unable to find identity {identity.Id} in device scopes cache or credentials cache"));
                                return await this.Connect(clientCredential, connectionStatusChangedHandler);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unable to find identity {identity.Id} in device scopes cache");
                            }
                        });
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        async Task<Try<ICloudConnection>> ConnectInternalWithDeviceStateTracking(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler, bool refreshCachedIdentity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            try
            {
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Id);
                string authChain = await this.deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(identity.Id, this.nestedEdgeEnabled, refreshCachedIdentity);

                return await this.TryCreateCloudConnectionFromServiceIdentity(identity, connectionStatusChangedHandler, refreshCachedIdentity, cloudListener, authChain);
            }
            catch (DeviceInvalidStateException ex)
            {
                return await this.TryRecoverCloudConnection(identity, connectionStatusChangedHandler, refreshCachedIdentity, ex);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                return Try<ICloudConnection>.Failure(ex);
            }
        }

        async Task<Try<ICloudConnection>> TryCreateCloudConnectionFromServiceIdentity(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler, bool refreshOutOfDateCache, CloudListener cloudListener, string authChain)
        {
            Events.CreatingCloudConnectionOnBehalfOf(identity);
            ConnectionMetadata connectionMetadata = await this.metadataStore.GetMetadata(identity.Id);
            string productInfo = connectionMetadata.EdgeProductInfo;
            Option<string> modelId = connectionMetadata.ModelId;

            ITransportSettings[] transportSettings = GetTransportSettings(
                   this.upstreamProtocol,
                   this.connectionPoolSize,
                   this.proxy,
                   this.useServerHeartbeat,
                   authChain);

            try
            {
                ICloudConnection cc = await CloudConnection.Create(
                               identity,
                               connectionStatusChangedHandler,
                               transportSettings,
                               this.messageConverterProvider,
                               this.clientProvider,
                               cloudListener,
                               this.edgeHubTokenProvider,
                               this.idleTimeout,
                               this.closeOnIdleTimeout,
                               this.operationTimeout,
                               productInfo,
                               modelId);
                Events.SuccessCreatingCloudConnection(identity);
                return Try.Success(cc);
            }
            catch (UnauthorizedException ex) when (this.trackDeviceState)
            {
                return await this.TryRecoverCloudConnection(identity, connectionStatusChangedHandler, refreshOutOfDateCache, ex);
            }
        }

        async Task<Try<ICloudConnection>> TryRecoverCloudConnection(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler, bool wasRefreshed, Exception ex)
        {
            try
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                if (this.scopeAuthenticationOnly && !this.edgeHubIdentity.Id.Equals(identity.Id))
                {
                    if (wasRefreshed)
                    {
                        Events.ErrorCreatingCloudConnection(identity, ex);
                        return Try<ICloudConnection>.Failure(ex);
                    }
                    else
                    {
                        // recover: try to update out of date cache and try again
                        return await this.ConnectInternalWithDeviceStateTracking(identity, connectionStatusChangedHandler, true);
                    }
                }
                else
                {
                    // try with cached device credentials if auth mode is not Scope or identity is for edgeHub
                    Events.ServiceIdentityNotFound(identity);
                    Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(identity);
                    var clientCredential = clientCredentials.Expect(() => new InvalidOperationException($"Unable to find identity {identity.Id} in device scopes cache or credentials cache"));
                    return await this.Connect(clientCredential, connectionStatusChangedHandler);
                }
            }
            catch (Exception e)
            {
                Events.ErrorCreatingCloudConnection(identity, e);
                return Try<ICloudConnection>.Failure(e);
            }
        }

        static ITransportSettings[] GetAmqpTransportSettings(TransportType type, int connectionPoolSize, Option<IWebProxy> proxy, bool useServerHeartbeat, string authChain)
        {
            var settings = new AmqpTransportSettings(type)
            {
                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                {
                    Pooling = true,
                    MaxPoolSize = (uint)connectionPoolSize
                }
            };

            if (useServerHeartbeat)
            {
                settings.IdleTimeout = HeartbeatTimeout;
            }

            proxy.ForEach(p => settings.Proxy = p);

            // Set the auth chain via Reflection
            settings.GetType()
                    .GetProperty("AuthenticationChain", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(settings, authChain);

            return new ITransportSettings[] { settings };
        }

        static ITransportSettings[] GetMqttTransportSettings(TransportType type, Option<IWebProxy> proxy, string authChain)
        {
            var settings = new MqttTransportSettings(type);
            proxy.ForEach(p => settings.Proxy = p);

            // Set the auth chain via Reflection
            settings.GetType()
                    .GetProperty("AuthenticationChain", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(settings, authChain);

            return new ITransportSettings[] { settings };
        }

        internal static ITransportSettings[] GetTransportSettings(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, Option<IWebProxy> proxy, bool useServerHeartbeat, string authChain)
        {
            return upstreamProtocol
                .Map(
                    up =>
                    {
                        switch (up)
                        {
                            case UpstreamProtocol.Amqp:
                                return GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, Option.None<IWebProxy>(), useServerHeartbeat, authChain);

                            case UpstreamProtocol.AmqpWs:
                                // Only WebSocket protocols can use an HTTP forward proxy
                                return GetAmqpTransportSettings(TransportType.Amqp_WebSocket_Only, connectionPoolSize, proxy, useServerHeartbeat, authChain);

                            case UpstreamProtocol.Mqtt:
                                return GetMqttTransportSettings(TransportType.Mqtt_Tcp_Only, Option.None<IWebProxy>(), authChain);

                            case UpstreamProtocol.MqttWs:
                                // Only WebSocket protocols can use an HTTP forward proxy
                                return GetMqttTransportSettings(TransportType.Mqtt_WebSocket_Only, proxy, authChain);

                            default:
                                throw new InvalidEnumArgumentException($"Unsupported transport type {up}");
                        }
                    })
                .GetOrElse(
                    () => GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, Option.None<IWebProxy>(), useServerHeartbeat, authChain));
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
