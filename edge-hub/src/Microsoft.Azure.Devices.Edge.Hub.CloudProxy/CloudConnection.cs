// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances)
    /// </summary>
    class CloudConnection : ICloudConnection
    {
        readonly ITransportSettings[] transportSettingsList;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly ICloudListener cloudListener;
        readonly TimeSpan idleTimeout;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan operationTimeout;
        Option<ICloudProxy> cloudProxy;

        protected CloudConnection(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ConnectionStatusChangedHandler = connectionStatusChangedHandler;
            this.transportSettingsList = Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.cloudProxy = Option.None<ICloudProxy>();
            this.operationTimeout = operationTimeout;
        }

        public static async Task<CloudConnection> Create(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            ITokenProvider tokenProvider,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout)
        {
            Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));
            var cloudConnection = new CloudConnection(
                identity,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout);
            ICloudProxy cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        public Option<ICloudProxy> CloudProxy => this.GetCloudProxy().Filter(cp => cp.IsActive);

        public bool IsActive => this.GetCloudProxy()
            .Map(cp => cp.IsActive)
            .GetOrElse(false);

        public Task<bool> CloseAsync() => this.GetCloudProxy().Map(cp => cp.CloseAsync()).GetOrElse(Task.FromResult(false));

        protected IIdentity Identity { get; }

        protected Action<string, CloudConnectionStatus> ConnectionStatusChangedHandler { get; }

        protected virtual Option<ICloudProxy> GetCloudProxy() => this.cloudProxy;

        protected async Task<ICloudProxy> CreateNewCloudProxyAsync(ITokenProvider newTokenProvider)
        {
            IClient client = await this.ConnectToIoTHub(newTokenProvider);
            ICloudProxy proxy = new CloudProxy(
                client,
                this.messageConverterProvider,
                this.Identity.Id,
                this.ConnectionStatusChangedHandler,
                this.cloudListener,
                this.idleTimeout,
                this.closeOnIdleTimeout);
            return proxy;
        }

        protected virtual bool CallbacksEnabled { get; } = true;

        async Task<IClient> ConnectToIoTHub(ITokenProvider newTokenProvider)
        {
            Events.AttemptingConnectionWithTransport(this.transportSettingsList, this.Identity);
            IClient client = this.clientProvider.Create(this.Identity, newTokenProvider, this.transportSettingsList);

            client.SetOperationTimeoutInMilliseconds((uint)this.operationTimeout.TotalMilliseconds);
            client.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangesHandler);

            // TODO: Add support for ProductInfo 
            //if (!string.IsNullOrWhiteSpace(newCredentials.ProductInfo))
            //{
            //    client.SetProductInfo(newCredentials.ProductInfo);
            //}

            await client.OpenAsync();
            Events.CreateDeviceClientSuccess(this.transportSettingsList, this.operationTimeout, this.Identity);
            return client;
        }

        void InternalConnectionStatusChangesHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            // Don't invoke the callbacks if callbacks are not enabled, i.e. when the
            // cloudProxy is being updated. That is because this method can be called before
            // this.CloudProxy has been set/updated, so the old CloudProxy object may be returned.
            if (this.CallbacksEnabled)
            {
                if (status == ConnectionStatus.Connected)
                {
                    this.ConnectionStatusChangedHandler?.Invoke(this.Identity.Id, CloudConnectionStatus.ConnectionEstablished);
                }
                else if (reason == ConnectionStatusChangeReason.Expired_SAS_Token)
                {
                    this.ConnectionStatusChangedHandler?.Invoke(this.Identity.Id, CloudConnectionStatus.DisconnectedTokenExpired);
                }
                else
                {
                    this.ConnectionStatusChangedHandler?.Invoke(this.Identity.Id, CloudConnectionStatus.Disconnected);
                }
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();
            const int IdStart = CloudProxyEventIds.CloudConnection;

            enum EventIds
            {
                AttemptingTransport = IdStart,
                TransportConnected
            }

            static string TransportName(TransportType type)
            {
                switch (type)
                {
                    case TransportType.Amqp_Tcp_Only:
                        return "AMQP";
                    case TransportType.Amqp_WebSocket_Only:
                        return "AMQP over WebSockets";
                    case TransportType.Mqtt_Tcp_Only:
                        return "MQTT";
                    case TransportType.Mqtt_WebSocket_Only:
                        return "MQTT over WebSockets";
                    default:
                        return type.ToString();
                }
            }

            public static void AttemptingConnectionWithTransport(ITransportSettings[] transportSettings, IIdentity identity)
            {
                string transportType = transportSettings.Length == 1
                    ? TransportName(transportSettings[0].GetTransportType())
                    : transportSettings.Select(t => TransportName(t.GetTransportType())).Join("/");
                Log.LogInformation((int)EventIds.AttemptingTransport, $"Attempting to connect to IoT Hub for client {identity.Id} via {transportType}...");
            }

            public static void CreateDeviceClientSuccess(ITransportSettings[] transportSettings, TimeSpan timeout, IIdentity identity)
            {
                string transportType = transportSettings.Length == 1
                    ? TransportName(transportSettings[0].GetTransportType())
                    : transportSettings.Select(t => TransportName(t.GetTransportType())).Join("/");
                Log.LogInformation((int)EventIds.TransportConnected, $"Created cloud proxy for client {identity.Id} via {transportType}, with client operation timeout {timeout.TotalSeconds} seconds.");
            }
        }
    }
}
