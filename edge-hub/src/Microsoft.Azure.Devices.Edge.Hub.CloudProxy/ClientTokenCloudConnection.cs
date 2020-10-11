// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances).
    /// </summary>
    internal class ClientTokenCloudConnection : CloudConnection, IClientTokenCloudConnection
    {
        readonly ClientTokenBasedTokenProvider tokenProvider;
        bool callbacksEnabled = true;

        ClientTokenCloudConnection(
            IIdentity identity,
            ClientTokenBasedTokenProvider tokenProvider,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            string productInfo,
            Option<string> modelId)
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
                productInfo,
                modelId)
        {
            this.tokenProvider = tokenProvider;
        }

        protected override bool CallbacksEnabled => this.callbacksEnabled;

        public static async Task<IClientTokenCloudConnection> Create(
            ITokenCredentials tokenCredentials,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            string productInfo,
            Option<string> modelId)
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));
            var tokenProvider = new ClientTokenBasedTokenProvider(tokenCredentials);
            var cloudConnection = new ClientTokenCloudConnection(
                tokenCredentials.Identity,
                tokenProvider,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout,
                operationTimeout,
                productInfo,
                modelId);
            try
            {
                var cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
                cloudConnection.cloudProxy = Option.Some(cloudProxy);
                return cloudConnection;
            }
            catch (Exception ex)
            {
                Events.Error($"Create ClientTokenCloudConnection failed for device {tokenCredentials.Identity} with token {tokenCredentials.Token} error: {ex}");
                throw;
            }
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
        /// <param name="tokenCredentials">New token credentials.</param>
        /// <returns>CloudProxy instance.</returns>
        public async Task<ICloudProxy> UpdateTokenAsync(ITokenCredentials tokenCredentials)
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));
            // Disable callbacks while we update the cloud proxy.
            this.callbacksEnabled = false;
            try
            {
                // First check if there is an existing cloud proxy
                ICloudProxy proxy = await this.CloudProxy.Map(cp =>
                    {
                        // If CloudProxy exists, just update tokenProvider with new token
                        this.tokenProvider.UpdateToken(tokenCredentials);
                        return Task.FromResult(cp);
                    })
                    // No existing cloud proxy, so just create a new one.
                    .GetOrElse(() => this.CreateNewCloudProxyAsync(this.tokenProvider));

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

        internal class ClientTokenBasedTokenProvider : ITokenProvider
        {
            ITokenCredentials tokenCredentials;

            public ClientTokenBasedTokenProvider(ITokenCredentials tokenCredentials)
            {
                if (IsTokenUsable(tokenCredentials))
                {
                    this.tokenCredentials = tokenCredentials;
                }
                else
                {
                    throw new ArgumentException("Invalid token.");
                }
            }

            internal void UpdateToken(ITokenCredentials tokenCredentials)
            {
                if (Equals(this.tokenCredentials.Identity, tokenCredentials.Identity))
                {
                    if (IsTokenUsable(tokenCredentials))
                    {
                        this.tokenCredentials = tokenCredentials;
                    }
                    else
                    {
                        Events.Error($"UpdateToken failed for device {tokenCredentials.Identity}.");
                        throw new ArgumentException("Invalid token.");
                    }
                }
                else
                {
                    Events.Error($"UpdateToken failed with invalid identity {tokenCredentials.Identity}, was {this.tokenCredentials.Identity}.");
                    throw new ArgumentException("Invalid token.");
                }
            }

            public Task<string> GetTokenAsync(Option<TimeSpan> ttl)
            {
                if (IsTokenUsable(this.tokenCredentials))
                {
                    return Task.FromResult(this.tokenCredentials.Token);
                }

                Events.Error($"GetTokenAsync failed for device {this.tokenCredentials.Identity}.");
                throw new AuthenticationException($"Unabled to get valid token for device {this.tokenCredentials.Identity}.");
            }

            // Checks if the token expires too soon
            static bool IsTokenUsable(ITokenCredentials tokenCredentials)
            {
                try
                {
                    if (TokenHelper.GetTokenExpiryTimeRemaining(tokenCredentials.Identity.IotHubHostName, tokenCredentials.Token) > TimeSpan.Zero)
                    {
                        return true;
                    }

                    Events.Error($"Expiring token {tokenCredentials.Token} for device {tokenCredentials.Identity}.");
                    return false;
                }
                catch (Exception e)
                {
                    Events.ErrorCheckingTokenUsable(e);
                    return false;
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
                ErrorCheckingTokenUsability,
                Error
            }

            public static void Error(string message)
            {
                Log.LogError((int)EventIds.Error, $"[ClientTokenCloudConnection] {message}.");
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
