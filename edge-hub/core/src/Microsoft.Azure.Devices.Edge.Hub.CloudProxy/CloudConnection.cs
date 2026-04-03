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
        readonly IotHubClientOptions clientOptions;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly IClientProvider clientProvider;
        readonly ICloudListener cloudListener;
        readonly TimeSpan idleTimeout;
        readonly bool closeOnIdleTimeout;
        readonly TimeSpan operationTimeout;
        readonly TimeSpan cloudConnectionHangingTimeout;
        readonly string productInfo;
        readonly Option<string> modelId;
        Option<ICloudProxy> cloudProxy;

        protected CloudConnection(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            IotHubClientOptions clientOptions,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            TimeSpan cloudConnectionHangingTimeout,
            string productInfo,
            Option<string> modelId)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ConnectionStatusChangedHandler = connectionStatusChangedHandler;
            this.clientOptions = Preconditions.CheckNotNull(clientOptions, nameof(clientOptions));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.cloudConnectionHangingTimeout = cloudConnectionHangingTimeout;
            this.cloudProxy = Option.None<ICloudProxy>();
            this.operationTimeout = operationTimeout;
            this.productInfo = productInfo;
            this.modelId = modelId;
        }

        public Option<ICloudProxy> CloudProxy => this.GetCloudProxy().Filter(cp => cp.IsActive);

        public bool IsActive => this.GetCloudProxy()
            .Map(cp => cp.IsActive)
            .GetOrElse(false);

        protected IIdentity Identity { get; }

        protected Action<string, CloudConnectionStatus> ConnectionStatusChangedHandler { get; }

        protected virtual bool CallbacksEnabled { get; } = true;

        public static async Task<CloudConnection> Create(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            IotHubClientOptions clientOptions,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            ITokenProvider tokenProvider,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            TimeSpan cloudConnectionHangingTimeout,
            string productInfo,
            Option<string> modelId)
        {
            Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));
            var cloudConnection = new CloudConnection(
                identity,
                connectionStatusChangedHandler,
                clientOptions,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout,
                cloudConnectionHangingTimeout,
                productInfo,
                modelId);
            ICloudProxy cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        public Task<bool> CloseAsync() => this.GetCloudProxy().Map(cp => cp.CloseAsync()).GetOrElse(Task.FromResult(false));

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
                this.closeOnIdleTimeout,
                this.cloudConnectionHangingTimeout);
            return proxy;
        }

        async Task<IClient> ConnectToIoTHub(ITokenProvider newTokenProvider)
        {
            Events.AttemptingConnectionWithTransport(this.clientOptions, this.Identity, this.modelId);
            IClient client = this.clientProvider.Create(this.Identity, newTokenProvider, this.clientOptions, this.modelId);

            // OperationTimeoutInMilliseconds is removed in v2 SDK; use CancellationToken-based timeouts instead
            client.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangesHandler);
            if (!string.IsNullOrWhiteSpace(this.productInfo))
            {
                client.SetProductInfo(this.productInfo);
            }

            await client.OpenAsync();
            Events.CreateDeviceClientSuccess(this.clientOptions, this.operationTimeout, this.Identity);
            return client;
        }

        void InternalConnectionStatusChangesHandler(ConnectionStatusInfo statusInfo)
        {
            // Don't invoke the callbacks if callbacks are not enabled, i.e. when the
            // cloudProxy is being updated. That is because this method can be called before
            // this.CloudProxy has been set/updated, so the old CloudProxy object may be returned.
            if (this.CallbacksEnabled)
            {
                if (statusInfo.Status == ConnectionStatus.Connected)
                {
                    this.ConnectionStatusChangedHandler?.Invoke(this.Identity.Id, CloudConnectionStatus.ConnectionEstablished);
                }
                else if (statusInfo.ChangeReason == ConnectionStatusChangeReason.ExpiredToken)
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
            const int IdStart = CloudProxyEventIds.CloudConnection;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();

            enum EventIds
            {
                AttemptingTransport = IdStart,
                TransportConnected
            }

            public static void AttemptingConnectionWithTransport(IotHubClientOptions clientOptions, IIdentity identity, Option<string> modelId)
            {
                string transportType = clientOptions.TransportSettings switch
                {
                    IotHubClientAmqpSettings amqp => amqp.Protocol == IotHubClientTransportProtocol.Tcp ? "AMQP" : "AMQP over WebSockets",
                    IotHubClientMqttSettings mqtt => mqtt.Protocol == IotHubClientTransportProtocol.Tcp ? "MQTT" : "MQTT over WebSockets",
                    _ => clientOptions.TransportSettings?.GetType().Name ?? "Unknown"
                };
                string message = $"Attempting to connect to IoT Hub for client {identity.Id} via {transportType}";
                string withModelIdMessage = modelId.Match(m => $" with modelId {m}", () => string.Empty);
                Log.LogInformation((int)EventIds.AttemptingTransport, $"{message}{withModelIdMessage}...");
            }

            public static void CreateDeviceClientSuccess(IotHubClientOptions clientOptions, TimeSpan timeout, IIdentity identity)
            {
                string transportType = clientOptions.TransportSettings switch
                {
                    IotHubClientAmqpSettings amqp => amqp.Protocol == IotHubClientTransportProtocol.Tcp ? "AMQP" : "AMQP over WebSockets",
                    IotHubClientMqttSettings mqtt => mqtt.Protocol == IotHubClientTransportProtocol.Tcp ? "MQTT" : "MQTT over WebSockets",
                    _ => clientOptions.TransportSettings?.GetType().Name ?? "Unknown"
                };
                Log.LogInformation((int)EventIds.TransportConnected, $"Created cloud proxy for client {identity.Id} via {transportType}, with client operation timeout {timeout.TotalSeconds} seconds.");
            }
        }
    }
}
