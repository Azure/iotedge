// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances)
    /// </summary>
    class CloudConnection : ICloudConnection
    {
        static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5); // Token is usable if it does not expire in 5 mins
        const int TokenTimeToLiveSeconds = 3600; // Unused - Token is generated by downstream clients
        const int TokenExpiryBufferPercentage = 8; // Assuming a standard token for 1 hr, we set expiry time to around 5 mins.
        const uint OperationTimeoutMilliseconds = 1 * 60 * 1000; // 1 min

        readonly Action<string, CloudConnectionStatus> connectionStatusChangedHandler;
        readonly ITransportSettings[] transportSettingsList;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly AsyncLock identityUpdateLock = new AsyncLock();
        readonly AsyncLock tokenUpdateLock = new AsyncLock();
        readonly IClientProvider clientProvider;

        bool callbacksEnabled = true;
        Option<TaskCompletionSource<string>> tokenGetter;
        Option<ICloudProxy> cloudProxy;
        Option<IIdentity> identity;

        public CloudConnection(Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider)
        {
            this.connectionStatusChangedHandler = connectionStatusChangedHandler;
            this.transportSettingsList = Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.tokenGetter = Option.None<TaskCompletionSource<string>>();
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
        }

        public Option<ICloudProxy> CloudProxy => this.cloudProxy.Filter(cp => cp.IsActive);

        public bool IsActive => this.cloudProxy
            .Map(cp => cp.IsActive)
            .GetOrElse(false);

        public Task<bool> CloseAsync() => this.cloudProxy.Map(cp => cp.CloseAsync()).GetOrElse(Task.FromResult(false));

        /// <summary>
        /// This method does the following -
        ///     1. Updates the identity to be used for the cloud connection
        ///     2. Updates the cloud proxy -
        ///         i. If there is an existing device client and 
        ///             a. If is waiting for an updated token, and the identity has a token,
        ///                then it uses that to give it to the waiting client authentication method.
        ///             b. If not, then it creates a new cloud proxy (and device client) and closes the existing one
        ///         ii. Else, if there is no cloud proxy, then opens a device client and creates a cloud proxy. 
        /// </summary>
        public async Task<ICloudProxy> CreateOrUpdateAsync(IClientCredentials newCredentials)
        {
            Preconditions.CheckNotNull(newCredentials, nameof(newCredentials));

            using (await this.identityUpdateLock.LockAsync())
            {
                // Disable callbacks while we update the cloud proxy.
                // TODO - instead of this, make convert Option<ICloudProxy> CloudProxy to Task<Option<ICloudProxy>> GetCloudProxy
                // which can be awaited when an update is in progress.
                this.callbacksEnabled = false;
                try
                {
                    // First check if there is an existing cloud proxy
                    (ICloudProxy proxy, bool newProxyCreated) = await this.cloudProxy.Map(
                        async cp =>
                        {
                            // If the identity has a token, and we have a tokenGetter, that means
                            // the connection is waiting for a new token. So give it the token and
                            // complete the tokenGetter
                            if (newCredentials is ITokenCredentials tokenAuth && this.tokenGetter.HasValue)
                            {
                                if (IsTokenExpired(tokenAuth.Identity.IotHubHostName, tokenAuth.Token))
                                {                                    
                                    throw new InvalidOperationException($"Token for client {tokenAuth.Identity.Id} is expired");
                                }

                                this.tokenGetter.ForEach(tg =>
                                 {
                                     tg.SetResult(tokenAuth.Token);
                                     this.tokenGetter = Option.None<TaskCompletionSource<string>>();
                                     Events.NewTokenObtained(newCredentials.Identity.IotHubHostName, newCredentials.Identity.Id, tokenAuth.Token);
                                 });
                                return (cp, false);
                            }
                            // Else this is a new connection for the same device Id. So open a new connection,
                            // and if that is successful, close the existing one.
                            else
                            {
                                ICloudProxy newCloudProxy = await this.GetCloudProxyAsync(newCredentials);
                                await cp.CloseAsync();
                                return (newCloudProxy, true);
                            }
                        })
                        // No existing cloud proxy, so just create a new one.
                        .GetOrElse(async () => (await this.GetCloudProxyAsync(newCredentials), true));

                    // Set identity only after successfully opening cloud proxy
                    // That way, if a we have one existing connection for a deviceA,
                    // and a new connection for deviceA comes in with an invalid key/token,
                    // the existing connection is not affected.
                    this.cloudProxy = Option.Some(proxy);
                    this.identity = Option.Some(newCredentials.Identity);
                    // Callbacks are disabled when updating identities, so explicitly invoke the
                    // connection status changed handler callback. 
                    if (newProxyCreated)
                    {
                        this.connectionStatusChangedHandler?.Invoke(newCredentials.Identity.Id, CloudConnectionStatus.ConnectionEstablished);
                    }
                    Events.UpdatedCloudConnection(newCredentials.Identity);
                    return proxy;
                }
                catch (Exception ex)
                {
                    Events.CreateException(ex, newCredentials.Identity);
                    throw;
                }
                finally
                {
                    this.callbacksEnabled = true;
                }
            }
        }

        async Task<ICloudProxy> GetCloudProxyAsync(IClientCredentials newCredentials)
        {
            IClient client = await this.ConnectToIoTHub(newCredentials);
            ICloudProxy proxy = new CloudProxy(client, this.messageConverterProvider, newCredentials.Identity.Id, this.connectionStatusChangedHandler);
            return proxy;
        }

        async Task<IClient> ConnectToIoTHub(IClientCredentials newCredentials)
        {
            Try<IClient> deviceClientTry = await Fallback.ExecuteAsync(
                this.transportSettingsList.Select<ITransportSettings, Func<Task<IClient>>>(
                    ts =>
                        () => this.CreateDeviceClient(newCredentials, ts)).ToArray());

            return deviceClientTry.Success ? deviceClientTry.Value : throw deviceClientTry.Exception;
        }

        async Task<IClient> CreateDeviceClient(
            IClientCredentials newCredentials,
            ITransportSettings transportSettings)
        {
            Events.AttemptingConnectionWithTransport(transportSettings.GetTransportType(), newCredentials.Identity);
            IClient client = await this.CreateDeviceClient(newCredentials, new[] { transportSettings }).ConfigureAwait(false);
            client.SetOperationTimeoutInMilliseconds(OperationTimeoutMilliseconds);
            client.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangesHandler);
            if (!string.IsNullOrWhiteSpace(newCredentials.ProductInfo))
            {
                client.SetProductInfo(newCredentials.ProductInfo);
            }
            
            Events.CreateDeviceClientSuccess(transportSettings.GetTransportType(), OperationTimeoutMilliseconds, newCredentials.Identity);
            return client;
        }

        Task<IClient> CreateDeviceClient(IClientCredentials newCredentials, ITransportSettings[] settings)
        {
            switch (newCredentials.AuthenticationType)
            {
                case AuthenticationType.SasKey:
                    if (!(newCredentials is ISharedKeyCredentials sharedKeyAuthentication))
                    {
                        throw new ArgumentException($"Sas key credential should be of type {nameof(ISharedKeyCredentials)}");
                    }
                    return Task.FromResult(this.clientProvider.Create(newCredentials.Identity, sharedKeyAuthentication.ConnectionString, settings));

                case AuthenticationType.Token:
                    if (!(newCredentials is ITokenCredentials tokenAuthentication))
                    {
                        throw new ArgumentException($"Token credential should be of type {nameof(ITokenCredentials)}");
                    }
                    IAuthenticationMethod authenticationMethod = this.GetAuthenticationMethod(tokenAuthentication.Identity, tokenAuthentication.Token);
                    return Task.FromResult(this.clientProvider.Create(newCredentials.Identity, authenticationMethod, settings));

                case AuthenticationType.IoTEdged:
                    if (!(newCredentials is IotEdgedCredentials))
                    {
                        throw new ArgumentException($"IoTEdged credential should be of type {nameof(IotEdgedCredentials)}");
                    }

                    return this.clientProvider.CreateAsync(newCredentials.Identity, settings);

                default:
                    throw new InvalidOperationException($"Unsupported authentication type {newCredentials.AuthenticationType}");
            }
        }

        IAuthenticationMethod GetAuthenticationMethod(IIdentity newIdentity, string token)
        {
            if (newIdentity is IModuleIdentity moduleIdentity)
            {
                var tokenRefresher = new ModuleTokenRefresher(moduleIdentity.DeviceId, moduleIdentity.ModuleId, token, this, newIdentity);
                return tokenRefresher;
            }
            else if (newIdentity is IDeviceIdentity deviceIdentity)
            {
                var tokenRefresher = new DeviceTokenRefresher(deviceIdentity.DeviceId, token, this, newIdentity);
                return tokenRefresher;
            }
            throw new InvalidOperationException($"Invalid client identity type {newIdentity.GetType()}");
        }

        void InternalConnectionStatusChangesHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            // Don't invoke the callbacks if callbacks are not enabled, i.e. when the
            // cloudProxy is being updated. That is because this method can be called before
            // this.CloudProxy has been set/updated, so the old CloudProxy object may be returned.
            if (this.callbacksEnabled)
            {
                this.identity.ForEach(currentIdentity =>
                {
                    if (status == ConnectionStatus.Connected)
                    {
                        this.connectionStatusChangedHandler?.Invoke(currentIdentity.Id, CloudConnectionStatus.ConnectionEstablished);
                    }
                    else if (reason == ConnectionStatusChangeReason.Expired_SAS_Token)
                    {
                        this.connectionStatusChangedHandler?.Invoke(currentIdentity.Id, CloudConnectionStatus.DisconnectedTokenExpired);
                    }
                    else
                    {
                        this.connectionStatusChangedHandler?.Invoke(currentIdentity.Id, CloudConnectionStatus.Disconnected);
                    }
                });
            }
        }

        /// <summary>
        /// If the existing identity has a usable token, then use it.
        /// Else, generate a notification of token being near expiry and return a task that
        /// can be completed later.
        /// Note - Don't use this.Identity in this method, as it may not have been set yet!
        /// </summary>
        async Task<string> GetNewToken(string iotHub, string id, string currentToken, IIdentity currentIdentity)
        {
            Events.GetNewToken(id);
            // We have to catch UnauthorizedAccessException, because on IsTokenUsable, we call parse from
            // Device Client and it throws if the token is expired.
            if (IsTokenUsable(iotHub, currentToken))
            {
                Events.UsingExistingToken(id);
                return currentToken;
            }
            else
            {
                Events.TokenExpired(id, currentToken);
            }

            // No need to lock here as the lock is being held by the refresher.
            TaskCompletionSource<string> tcs = this.tokenGetter
                .GetOrElse(
                    () =>
                    {
                        Events.SafeCreateNewToken(id);
                        var taskCompletionSource = new TaskCompletionSource<string>();
                        this.tokenGetter = Option.Some(taskCompletionSource);
                        this.connectionStatusChangedHandler(currentIdentity.Id, CloudConnectionStatus.TokenNearExpiry);
                        return taskCompletionSource;
                    });
            string newToken = await tcs.Task;
            return newToken;
        }


        internal static DateTime GetTokenExpiry(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                return expiryTime;
            }
            catch (UnauthorizedAccessException)
            {
                return DateTime.MinValue;
            }
        }

        internal static bool IsTokenExpired(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                return sharedAccessSignature.IsExpired();
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        internal static TimeSpan GetTokenExpiryTimeRemaining(string hostName, string token) => GetTokenExpiry(hostName, token) - DateTime.UtcNow;

        // Checks if the token expires too soon
        static bool IsTokenUsable(string hostname, string token)
        {
            try
            {
                return GetTokenExpiryTimeRemaining(hostname, token) > TokenExpiryBuffer;
            }
            catch (Exception e)
            {
                Events.ErrorCheckingTokenUsable(e);
                return false;
            }
        }

        class DeviceTokenRefresher : DeviceAuthenticationWithTokenRefresh
        {
            readonly CloudConnection cloudConnection;
            readonly IIdentity identity;
            string token;

            public DeviceTokenRefresher(string deviceId, string token, CloudConnection cloudConnection, IIdentity identity)
                : base(deviceId, TokenTimeToLiveSeconds, TokenExpiryBufferPercentage)
            {
                this.cloudConnection = cloudConnection;
                this.token = token;
                this.identity = identity;
            }

            protected override async Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
            {
                using (await this.cloudConnection.tokenUpdateLock.LockAsync())
                {
                    try
                    {
                        this.token = await this.cloudConnection.GetNewToken(iotHub, this.DeviceId, this.token, this.identity);
                        return this.token;
                    }
                    catch (Exception ex)
                    {
                        Events.ErrorRenewingToken(ex);
                        throw;
                    }

                }
            }
        }

        class ModuleTokenRefresher : ModuleAuthenticationWithTokenRefresh
        {
            readonly CloudConnection cloudConnection;
            readonly IIdentity identity;
            string token;

            public ModuleTokenRefresher(string deviceId, string moduleId, string token, CloudConnection cloudConnection, IIdentity identity)
                : base(deviceId, moduleId, TokenTimeToLiveSeconds, TokenExpiryBufferPercentage)
            {
                this.cloudConnection = cloudConnection;
                this.token = token;
                this.identity = identity;
            }

            protected override async Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
            {
                using (await this.cloudConnection.tokenUpdateLock.LockAsync())
                {
                    try
                    {
                        this.token = await this.cloudConnection.GetNewToken(iotHub, $"{this.DeviceId}/{this.ModuleId}", this.token, this.identity);
                        return this.token;
                    }
                    catch (Exception ex)
                    {
                        Events.ErrorRenewingToken(ex);
                        throw;
                    }
                }
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();
            const int IdStart = CloudProxyEventIds.CloudConnection;

            enum EventIds
            {
                CloudConnectError = IdStart,
                AttemptingTransport,
                TransportConnected,
                CreateNewToken,
                UpdatedCloudConnection,
                ObtainedNewToken,
                TokenExpired,
                ErrorRenewingToken,
                ErrorCheckingTokenUsability
            }

            static string TransportName(TransportType type)
            {
                switch (type)
                {
                    case TransportType.Amqp_Tcp_Only:
                        return "AMQP";
                    case TransportType.Amqp_WebSocket_Only:
                        return "AMQP over WebSocket";
                    default:
                        return type.ToString();
                }
            }

            public static void AttemptingConnectionWithTransport(TransportType transport, IIdentity identity)
            {
                Log.LogInformation((int)EventIds.AttemptingTransport, $"Attempting to connect to IoT Hub for client {identity.Id} via {TransportName(transport)}...");
            }

            public static void CreateDeviceClientSuccess(TransportType transport, uint timeout, IIdentity identity)
            {
                Log.LogInformation((int)EventIds.TransportConnected, $"Connected to IoT Hub for client {identity.Id} via {TransportName(transport)}, with client operation timeout {timeout}.");
            }

            internal static void GetNewToken(string id)
            {
                Log.LogDebug((int)EventIds.CreateNewToken, Invariant($"Getting new token for {id}."));
            }

            internal static void UsingExistingToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"New token requested by client {id}, but using existing token as it is usable."));
            }

            internal static void SafeCreateNewToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"Existing token not found for {id}. Getting new token from the client..."));
            }

            internal static void CreateException(Exception ex, IIdentity identity)
            {
                Log.LogError((int)EventIds.CloudConnectError, ex, Invariant($"Error creating or updating the cloud proxy for client {identity.Id}"));
            }

            internal static void UpdatedCloudConnection(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.UpdatedCloudConnection, Invariant($"Updated cloud connection for client {identity.Id}"));
            }

            internal static void NewTokenObtained(string hostname, string id, string newToken)
            {
                TimeSpan timeRemaining = GetTokenExpiryTimeRemaining(hostname, newToken);
                Log.LogInformation((int)EventIds.ObtainedNewToken, Invariant($"Obtained new token for client {id} that expires in {timeRemaining}"));
            }

            internal static void TokenExpired(string id, string currentToken)
            {
                Log.LogDebug((int)EventIds.TokenExpired, Invariant($"Token Expired. Id:{id}, CurrentToken: {currentToken}."));
            }

            internal static void ErrorRenewingToken(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorRenewingToken, ex, "Critical Error trying to renew Token.");
            }

            public static void ErrorCheckingTokenUsable(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorCheckingTokenUsability, ex, "Error checking if token is usable.");
            }
        }
    }
}
