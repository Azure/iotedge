// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
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

        bool callbacksEnabled = true;
        Option<TaskCompletionSource<string>> tokenGetter;
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
            string productInfo)
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
                productInfo)
        {
        }

        protected override bool CallbacksEnabled => this.callbacksEnabled;

        public static async Task<ClientTokenCloudConnection> Create(
            ITokenCredentials tokenCredentials,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            string productInfo)
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));
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
                productInfo);
            ITokenProvider tokenProvider = new ClientTokenBasedTokenProvider(tokenCredentials, cloudConnection);
            ICloudProxy cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        /// <summary>
        /// This method does the following -
        ///     1. Updates the Identity to be used for the cloud connection
        ///     2. Updates the cloud proxy -
        ///         i. If there is an existing device client and
        ///             a. If is waiting for an updated token, and the Identity has a token,
        ///                then it uses that to give it to the waiting client authentication method.
        ///             b. If not, then it creates a new cloud proxy (and device client) and closes the existing one
        ///         ii. Else, if there is no cloud proxy, then opens a device client and creates a cloud proxy.
        /// </summary>
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
                                if (this.tokenGetter.HasValue)
                                {
                                    if (TokenHelper.IsTokenExpired(this.Identity.IotHubHostName, newTokenCredentials.Token))
                                    {
                                        throw new InvalidOperationException($"Token for client {this.Identity.Id} is expired");
                                    }

                                    this.tokenGetter.ForEach(
                                        tg =>
                                        {
                                            // First reset the token getter and then set the result.
                                            this.tokenGetter = Option.None<TaskCompletionSource<string>>();
                                            tg.SetResult(newTokenCredentials.Token);
                                        });
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
                        // No existing cloud proxy, so just create a new one.
                        .GetOrElse(() => this.CreateNewCloudProxyAsync(tokenProvider));

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
        /// If the existing Identity has a usable token, then use it.
        /// Else, generate a notification of token being near expiry and return a task that
        /// can be completed later.
        /// Keep retrying till we get a usable token.
        /// Note - Don't use this.Identity in this method, as it may not have been set yet!
        /// </summary>
        async Task<string> GetNewToken(string currentToken)
        {
            Events.GetNewToken(this.Identity.Id);
            bool retrying = false;
            string token = currentToken;
            while (true)
            {
                // We have to catch UnauthorizedAccessException, because on IsTokenUsable, we call parse from
                // Device Client and it throws if the token is expired.
                if (IsTokenUsable(this.Identity.IotHubHostName, token))
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
                else
                {
                    Events.TokenNotUsable(this.Identity, token);
                }

                bool newTokenGetterCreated = false;
                // No need to lock here as the lock is being held by the refresher.
                TaskCompletionSource<string> tcs = this.tokenGetter
                    .GetOrElse(
                        () =>
                        {
                            Events.SafeCreateNewToken(this.Identity.Id);
                            var taskCompletionSource = new TaskCompletionSource<string>();
                            this.tokenGetter = Option.Some(taskCompletionSource);
                            newTokenGetterCreated = true;
                            return taskCompletionSource;
                        });

                // If a new tokenGetter was created, then invoke the connection status changed handler
                if (newTokenGetterCreated)
                {
                    // If retrying, wait for some time.
                    if (retrying)
                    {
                        await Task.Delay(TokenRetryWaitTime);
                    }

                    this.ConnectionStatusChangedHandler(this.Identity.Id, CloudConnectionStatus.TokenNearExpiry);
                }

                retrying = true;
                // this.tokenGetter will be reset when this task returns.
                token = await tcs.Task;
            }
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
                TimeSpan timeRemaining = TokenHelper.GetTokenExpiryTimeRemaining(identity.IotHubHostName, newToken);
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
                TimeSpan timeRemaining = TokenHelper.GetTokenExpiryTimeRemaining(identity.IotHubHostName, newToken);
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
