// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    public class CloudConnectionProvider : ICloudConnectionProvider
    {
        static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(60);
        static readonly TimeSpan DefaultSasTokenTTL = TimeSpan.FromHours(1);

        readonly ITransportSettings[] transportSettings;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly TimeSpan idleTimeout;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly bool closeOnIdleTimeout;
        readonly ICredentialsCache credentialsCache;
        readonly TimeSpan operationTimeout;
        readonly IMetadataStore metadataStore;
        Option<IEdgeHub> edgeHub;

        public CloudConnectionProvider(
            IMessageConverterProvider messageConverterProvider,
            int connectionPoolSize,
            IClientProvider clientProvider,
            Option<UpstreamProtocol> upstreamProtocol,
            ITokenProvider edgeHubTokenProvider,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            ICredentialsCache credentialsCache,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            bool useServerHeartbeat,
            Option<IWebProxy> proxy,
            IMetadataStore metadataStore)
        {
            Preconditions.CheckRange(connectionPoolSize, 1, nameof(connectionPoolSize));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.transportSettings = GetTransportSettings(upstreamProtocol, connectionPoolSize, proxy, useServerHeartbeat);
            this.edgeHub = Option.None<IEdgeHub>();
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.operationTimeout = operationTimeout;
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));
        }

        public void BindEdgeHub(IEdgeHub edgeHubInstance)
        {
            this.edgeHub = Option.Some(Preconditions.CheckNotNull(edgeHubInstance, nameof(edgeHubInstance)));
        }

        async Task<ICloudConnection> ConnectWithoutScopeAsync(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            string productInfo,
            Option<string> modelId,
            Option<IClientCredentials> credentials)
        {
            Events.CreatingCloudConnectionUsingClientCredentials(identity);
            if (!credentials.HasValue)
            {
                credentials = await this.credentialsCache.Get(identity);
            }
            else if (!Equals(credentials.OrDefault().Identity, identity))
            {
                throw new ArgumentException("Invalid credentials");
            }

            var deviceCredentials = credentials.Expect(() => new AuthenticationException($"Unabled to get credentials for device {identity}"));
            var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Id);

            ICloudConnection cloudConnection = null;
            if (deviceCredentials is TokenCredentials tokenCredentials)
            {
                cloudConnection = await ClientTokenCloudConnection.Create(
                    tokenCredentials,
                    connectionStatusChangedHandler,
                    this.transportSettings,
                    this.messageConverterProvider,
                    this.clientProvider,
                    cloudListener,
                    this.idleTimeout,
                    this.closeOnIdleTimeout,
                    this.operationTimeout,
                    productInfo,
                    modelId);
            }
            else if (deviceCredentials is SharedKeyCredentials sharedKeyCredentials)
            {
                var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(sharedKeyCredentials.ConnectionString);
                var signatureProvider = new SharedAccessKeySignatureProvider(iotHubConnectionStringBuilder.SharedAccessKey);
                var tokenProvider = new ClientTokenProvider(signatureProvider, iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId, DefaultSasTokenTTL);
                cloudConnection = await CloudConnection.Create(
                    identity,
                    connectionStatusChangedHandler,
                    this.transportSettings,
                    this.messageConverterProvider,
                    this.clientProvider,
                    cloudListener,
                    tokenProvider,
                    this.idleTimeout,
                    this.closeOnIdleTimeout,
                    this.operationTimeout,
                    productInfo,
                    modelId);
            }
            else
            {
                throw new AuthenticationException($"Unabled to connectwith credentials type {deviceCredentials.GetType()} for device {identity}");
            }

            Events.SuccessCreatingCloudConnection(identity);
            return cloudConnection;
        }

        public Task<ITry<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            Preconditions.CheckNotNull(clientCredentials, nameof(clientCredentials));
            return Option.Some(clientCredentials)
                .Map(cr => cr as ITokenCredentials)
                .Map(tcr => this.ConnectAsync(tcr.Identity, connectionStatusChangedHandler, Option.Some(clientCredentials)))
                .GetOrElse(() => this.Connect(clientCredentials.Identity, connectionStatusChangedHandler));
        }

        public Task<ITry<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler) => this.ConnectAsync(identity, connectionStatusChangedHandler, Option.None<IClientCredentials>());

        async Task<ITry<ICloudConnection>> ConnectAsync(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler, Option<IClientCredentials> initialCredentials)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            try
            {
                ConnectionMetadata connectionMetadata = await this.metadataStore.GetMetadata(identity.Id);
                string productInfo = connectionMetadata.EdgeProductInfo;
                Option<string> modelId = connectionMetadata.ModelId;

                var serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(identity.Id, true);
                var cloudConnectionCreationTask = serviceIdentity.Map(si => this.ConnectOnBehalfOfAsync(si, identity, connectionStatusChangedHandler, productInfo, modelId))
                    .GetOrElse(() => this.ConnectWithoutScopeAsync(identity, connectionStatusChangedHandler, productInfo, modelId, initialCredentials));

                var cloudConnection = await cloudConnectionCreationTask;
                return Try.Success(cloudConnection);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingCloudConnection(identity, ex);
                return Try.Failure<ICloudConnection>(ex);
            }
        }

        async Task<ICloudConnection> ConnectOnBehalfOfAsync(
            ServiceIdentity serviceIdentity,
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            string productInfo,
            Option<string> modelId)
        {
            if (serviceIdentity.Status == ServiceIdentityStatus.Enabled)
            {
                Events.CreatingCloudConnectionOnBehalfOf(identity);
                var cloudListener = new CloudListener(this.edgeHub.Expect(() => new InvalidOperationException("EdgeHub reference should not be null")), identity.Id);
                try
                {
                    ICloudConnection cloudConnection = await CloudConnection.Create(
                        identity,
                        connectionStatusChangedHandler,
                        this.transportSettings,
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
                    return cloudConnection;
                }
                catch (Exception ex) when (ex is DeviceNotFoundException || ex is UnauthorizedException || ex is AuthenticationException)
                {
                    // if connection got rejected with not found or unauthorized, we need to trigger device scope identity refresh for this id
                    await this.deviceScopeIdentitiesCache.RefreshServiceIdentity(identity.Id);
                    throw;
                }
            }
            else
            {
                throw new AuthenticationException($"Device {identity.Id} state is {serviceIdentity.Status}.");
            }
        }

        static ITransportSettings[] GetAmqpTransportSettings(TransportType type, int connectionPoolSize, Option<IWebProxy> proxy, bool useServerHeartbeat)
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
            return new ITransportSettings[] { settings };
        }

        static ITransportSettings[] GetMqttTransportSettings(TransportType type, Option<IWebProxy> proxy)
        {
            var settings = new MqttTransportSettings(type);
            proxy.ForEach(p => settings.Proxy = p);
            return new ITransportSettings[] { settings };
        }

        internal static ITransportSettings[] GetTransportSettings(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, Option<IWebProxy> proxy, bool useServerHeartbeat)
        {
            return upstreamProtocol
                .Map(
                    up =>
                    {
                        switch (up)
                        {
                            case UpstreamProtocol.Amqp:
                                return GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, proxy, useServerHeartbeat);

                            case UpstreamProtocol.AmqpWs:
                                return GetAmqpTransportSettings(TransportType.Amqp_WebSocket_Only, connectionPoolSize, proxy, useServerHeartbeat);

                            case UpstreamProtocol.Mqtt:
                                return GetMqttTransportSettings(TransportType.Mqtt_Tcp_Only, proxy);

                            case UpstreamProtocol.MqttWs:
                                return GetMqttTransportSettings(TransportType.Mqtt_WebSocket_Only, proxy);

                            default:
                                throw new InvalidEnumArgumentException($"Unsupported transport type {up}");
                        }
                    })
                .GetOrElse(
                    () => GetAmqpTransportSettings(TransportType.Amqp_Tcp_Only, connectionPoolSize, proxy, useServerHeartbeat));
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

            public static void CreatingCloudConnectionUsingClientCredentials(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.CreatingCloudConnectionUsingClientCredentials, $"Creating cloud connection for client {identity.Id} using client credentials");
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
