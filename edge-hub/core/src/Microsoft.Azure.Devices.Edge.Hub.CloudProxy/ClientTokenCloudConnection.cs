// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances).
    /// </summary>
    class ClientTokenCloudConnection : CloudConnection, IClientTokenCloudConnection
    {
        static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5); // Token is usable if it does not expire in 5 mins
        static readonly TimeSpan TokenRetryWaitTime = TimeSpan.FromSeconds(20);

        readonly AsyncLock identityUpdateLock = new AsyncLock();
        readonly Func<Task> tokenRetryDelay;

        bool callbacksEnabled = true;
        TaskCompletionSource<string> tokenGetter;
        int tokenUpdatesCanceled;
        Option<ICloudProxy> cloudProxy;

        ClientTokenCloudConnection(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            TimeSpan cloudConnectionHangingTimeout,
            string productInfo,
            Option<string> modelId,
            Func<Task> tokenRetryDelay)
            : base(
                identity,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout,
                cloudConnectionHangingTimeout,
                productInfo,
                modelId)
        {
            this.tokenRetryDelay = tokenRetryDelay;
        }

        protected override bool CallbacksEnabled => this.callbacksEnabled;

        public static Task<ClientTokenCloudConnection> Create(
            ITokenCredentials tokenCredentials,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            TimeSpan cloudConnectionHangingTimeout,
            string productInfo,
            Option<string> modelId)
            => Create(
                tokenCredentials,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout,
                cloudConnectionHangingTimeout,
                productInfo,
                modelId,
                () => Task.Delay(TokenRetryWaitTime));

        internal static async Task<ClientTokenCloudConnection> Create(
            ITokenCredentials tokenCredentials,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            TimeSpan cloudConnectionHangingTimeout,
            string productInfo,
            Option<string> modelId,
            Func<Task> tokenRetryDelay)
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));
            Preconditions.CheckNotNull(tokenRetryDelay, nameof(tokenRetryDelay));
            var cloudConnection = new ClientTokenCloudConnection(
                tokenCredentials.Identity,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout,
                cloudConnectionHangingTimeout,
                productInfo,
                modelId,
                tokenRetryDelay);
            ITokenProvider tokenProvider = new ClientTokenBasedTokenProvider(tokenCredentials, cloudConnection);
            ICloudProxy cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        /// <summary>
        /// Applies new token credentials to the cloud connection.
        /// </summary>
        /// <remarks>
        /// A pending token request reuses the existing proxy. Otherwise, a replacement proxy opens
        /// before the existing proxy closes so invalid credentials do not disrupt an active connection.
        /// </remarks>
        /// <param name="newTokenCredentials">New token credentials.</param>
        /// <returns>The active cloud proxy.</returns>
        public async Task<ICloudProxy> UpdateTokenAsync(ITokenCredentials newTokenCredentials)
        {
            Preconditions.CheckNotNull(newTokenCredentials, nameof(newTokenCredentials));

            using (await this.identityUpdateLock.LockAsync())
            {
                // Disable callbacks while we update the cloud proxy.
                // TODO - instead of this, make convert Option<ICloudProxy> CloudProxy to Task<Option<ICloudProxy>> GetCloudProxy
                // which can be awaited when an update is in progress.
                this.callbacksEnabled = false;
                try
                {
                    ITokenProvider tokenProvider = new ClientTokenBasedTokenProvider(newTokenCredentials, this);
                    // First check if there is an existing cloud proxy
                    ICloudProxy proxy = await this.CloudProxy.Map(
                            async cp =>
                            {
                                // If the Identity has a token, and we have a tokenGetter, that means
                                // the connection is waiting for a new token. So give it the token and
                                // complete the tokenGetter
                                if (Volatile.Read(ref this.tokenGetter) != null)
                                {
                                    this.CompleteTokenGetter(newTokenCredentials);
                                    return cp;
                                }
                                else
                                {
                                    // Else this is a new connection for the same device Id. So open a new connection,
                                    // and if that is successful, close the existing one.
                                    ICloudProxy newCloudProxy = await this.CreateNewCloudProxyAsync(tokenProvider);
                                    await cp.CloseAsync();
                                    return newCloudProxy;
                                }
                            })
                        .GetOrElse(
                            async () =>
                            {
                                this.CompleteTokenGetter(newTokenCredentials);
                                return await this.CreateNewCloudProxyAsync(tokenProvider);
                            });

                    // Set Identity only after successfully opening cloud proxy
                    // That way, if a we have one existing connection for a deviceA,
                    // and a new connection for deviceA comes in with an invalid key/token,
                    // the existing connection is not affected.
                    this.cloudProxy = Option.Some(proxy);
                    Events.UpdatedCloudConnection(this.Identity);
                    return proxy;
                }
                catch (Exception ex)
                {
                    Events.CreateException(ex, this.Identity);
                    throw;
                }
                finally
                {
                    this.callbacksEnabled = true;
                }
            }
        }

        protected override Option<ICloudProxy> GetCloudProxy() => this.cloudProxy;

        public bool HasPendingTokenUpdate => Volatile.Read(ref this.tokenGetter) != null;

        public void CancelTokenUpdate()
        {
            Volatile.Write(ref this.tokenUpdatesCanceled, 1);
            Interlocked.Exchange(ref this.tokenGetter, null)?.TrySetCanceled();
        }

        void CompleteTokenGetter(ITokenCredentials newTokenCredentials)
        {
            TaskCompletionSource<string> currentTokenGetter = Volatile.Read(ref this.tokenGetter);
            if (currentTokenGetter != null
                && TokenHelper.IsTokenExpired(this.Identity.IotHubHostname, newTokenCredentials.Token))
            {
                throw new InvalidOperationException($"Token for client {this.Identity.Id} is expired");
            }

            Interlocked.Exchange(ref this.tokenGetter, null)?.TrySetResult(newTokenCredentials.Token);
        }

        // Checks if the token expires too soon
        static bool IsTokenUsable(string hostname, string token)
        {
            try
            {
                return TokenHelper.GetTokenExpiryTimeRemaining(hostname, token) > TokenExpiryBuffer;
            }
            catch (Exception e)
            {
                Events.ErrorCheckingTokenUsable(e);
                return false;
            }
        }

        /// <summary>
        /// Returns the supplied token when usable; otherwise, requests replacement tokens until one is usable.
        /// </summary>
        /// <remarks>
        /// Token validation uses the supplied value because the connection identity may not yet contain
        /// the latest credentials.
        /// </remarks>
        /// <param name="currentToken">The token to validate first.</param>
        /// <returns>A usable token.</returns>
        /// <exception cref="OperationCanceledException">Token updates have been canceled.</exception>
        async Task<string> GetNewToken(string currentToken)
        {
            Events.GetNewToken(this.Identity.Id);
            bool retrying = false;
            string token = currentToken;
            while (true)
            {
                if (IsTokenUsable(this.Identity.IotHubHostname, token))
                {
                    if (retrying)
                    {
                        Events.NewTokenObtained(this.Identity, token);
                    }
                    else
                    {
                        Events.UsingExistingToken(this.Identity.Id);
                    }

                    return token;
                }

                Events.TokenNotUsable(this.Identity, token);

                bool tokenGetterPublished = false;
                this.ThrowIfTokenUpdatesCanceled(cancelPublishedWaiter: false);
                TaskCompletionSource<string> tcs = Volatile.Read(ref this.tokenGetter);
                if (tcs == null)
                {
                    Events.SafeCreateNewToken(this.Identity.Id);
                    var taskCompletionSource = new TaskCompletionSource<string>();
                    tcs = Interlocked.CompareExchange(
                        ref this.tokenGetter,
                        taskCompletionSource,
                        null);
                    if (tcs == null)
                    {
                        tcs = taskCompletionSource;
                        tokenGetterPublished = true;
                    }
                }

                this.ThrowIfTokenUpdatesCanceled(cancelPublishedWaiter: true);

                if (tokenGetterPublished)
                {
                    if (retrying)
                    {
                        await this.tokenRetryDelay();
                    }

                    this.ThrowIfTokenUpdatesCanceled(cancelPublishedWaiter: true);
                    this.ConnectionStatusChangedHandler(this.Identity.Id, CloudConnectionStatus.TokenNearExpiry);
                }

                retrying = true;
                token = await tcs.Task;
            }
        }

        void ThrowIfTokenUpdatesCanceled(bool cancelPublishedWaiter)
        {
            if (Volatile.Read(ref this.tokenUpdatesCanceled) == 0)
            {
                return;
            }

            if (cancelPublishedWaiter)
            {
                Interlocked.Exchange(ref this.tokenGetter, null)?.TrySetCanceled();
            }

            throw new OperationCanceledException(
                $"Token updates for client {this.Identity.Id} have been canceled.");
        }

        class ClientTokenBasedTokenProvider : ITokenProvider
        {
            readonly ClientTokenCloudConnection cloudConnection;
            readonly AsyncLock tokenUpdateLock = new AsyncLock();
            string token;

            public ClientTokenBasedTokenProvider(ITokenCredentials tokenCredentials, ClientTokenCloudConnection cloudConnection)
            {
                this.cloudConnection = cloudConnection;
                this.token = tokenCredentials.Token;
            }

            public async Task<string> GetTokenAsync(Option<TimeSpan> ttl)
            {
                using (await this.tokenUpdateLock.LockAsync())
                {
                    try
                    {
                        this.token = await this.cloudConnection.GetNewToken(this.token);
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
            const int IdStart = CloudProxyEventIds.CloudConnection;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ClientTokenCloudConnection>();

            enum EventIds
            {
                CloudConnectError = IdStart,
                CreateNewToken,
                UpdatedCloudConnection,
                ObtainedNewToken,
                ErrorRenewingToken,
                ErrorCheckingTokenUsability
            }

            public static void ErrorCheckingTokenUsable(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorCheckingTokenUsability, ex, "Error checking if token is usable.");
            }

            public static void TokenNotUsable(IIdentity identity, string newToken)
            {
                TimeSpan timeRemaining = TokenHelper.GetTokenExpiryTimeRemaining(identity.IotHubHostname, newToken);
                Log.LogDebug((int)EventIds.ObtainedNewToken, Invariant($"Token received for client {identity.Id} expires in {timeRemaining}, and so is not usable. Getting a fresh token..."));
            }

            internal static void CreateException(Exception ex, IIdentity identity)
            {
                Log.LogError((int)EventIds.CloudConnectError, ex, Invariant($"Error creating or updating the cloud proxy for client {identity.Id}"));
            }

            internal static void ErrorRenewingToken(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorRenewingToken, ex, "Critical Error trying to renew Token.");
            }

            internal static void GetNewToken(string id)
            {
                Log.LogDebug((int)EventIds.CreateNewToken, Invariant($"Getting new token for {id}."));
            }

            internal static void NewTokenObtained(IIdentity identity, string newToken)
            {
                TimeSpan timeRemaining = TokenHelper.GetTokenExpiryTimeRemaining(identity.IotHubHostname, newToken);
                Log.LogInformation((int)EventIds.ObtainedNewToken, Invariant($"Obtained new token for client {identity.Id} that expires in {timeRemaining}"));
            }

            internal static void SafeCreateNewToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"Existing token not found for {id}. Getting new token from the client..."));
            }

            internal static void UpdatedCloudConnection(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.UpdatedCloudConnection, Invariant($"Updated cloud connection for client {identity.Id}"));
            }

            internal static void UsingExistingToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"New token requested by client {id}, but using existing token as it is usable."));
            }
        }
    }
}
