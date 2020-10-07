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
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances).
    /// </summary>
    class ClientTokenCloudConnection : CloudConnection, IClientTokenCloudConnection
    {
        static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5); // Token is usable if it does not expire in 5 mins
        static readonly TimeSpan PollBuffer = TimeSpan.FromSeconds(20);

        readonly ClientTokenBasedTokenProvider tokenProvider;
        bool callbacksEnabled = true;
        Option<ICloudProxy> cloudProxy;

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
            IIdentity identity,
            ICredentialsCache credentialsCache,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout,
            TimeSpan operationTimeout,
            string productInfo,
            Option<string> modelId,
            Option<string> initialToken)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            var tokenProvider = new ClientTokenBasedTokenProvider(identity, credentialsCache, initialToken);
            var cloudConnection = new ClientTokenCloudConnection(
                identity,
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
            Events.Debugging($"Before create new ClientTokenCloudConnection for device {identity.Id}.");
            var cloudProxy = await cloudConnection.CreateNewCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            Events.Debugging($"After create new ClientTokenCloudConnection for device {identity.Id}.");
            Events.Debugging($"Associated ClientTokenCloudConnection({cloudConnection.GetHashCode()}) to {identity.Id}.");
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
        /// <param name="tokenCredentials">New token credentials.</param>
        /// <returns>CloudProxy instance.</returns>
        public async Task<ICloudProxy> UpdateTokenAsync(ITokenCredentials tokenCredentials)
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));
            // Disable callbacks while we update the cloud proxy.
            // TODO - instead of this, make convert Option<ICloudProxy> CloudProxy to Task<Option<ICloudProxy>> GetCloudProxy
            // which can be awaited when an update is in progress.
            this.callbacksEnabled = false;
            try
            {
                // First check if there is an existing cloud proxy
                ICloudProxy proxy = await this.CloudProxy.Map(cp =>
                    {
                        // If CloudProxy exists, just update tokenProvider with new token
                        this.tokenProvider.UpdateToken(tokenCredentials.Token);
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

        protected override Option<ICloudProxy> GetCloudProxy() => this.cloudProxy;

        internal class ClientTokenBasedTokenProvider : ITokenProvider
        {
            IIdentity identity;
            ICredentialsCache credentialsCache;
            Option<string> cachedToken;
            Option<DateTime> firstFailureTime;

            public ClientTokenBasedTokenProvider(IIdentity identity, ICredentialsCache credentialsCache, Option<string> initialToken)
            {
                this.identity = identity;
                this.cachedToken = initialToken;
                this.credentialsCache = credentialsCache;
                this.firstFailureTime = Option.None<DateTime>();
            }

            internal void UpdateToken(string token)
            {
                Events.Debugging($"Before UpdateToken for device {this.identity.Id} to token={token}.");
                if (IsTokenUsable(this.identity.IotHubHostName, token))
                {
                    this.cachedToken = Option.Some(token);
                    Events.Debugging($"After UpdateToken for device {this.identity.Id} to token={token}.");
                }
                else
                {
                    Events.Debugging($"After UpdateToken for device {this.identity.Id} failed with invalid token={token}.");
                    throw new ArgumentException("Invalid token.");
                }
            }

            public async Task<string> GetTokenAsync(Option<TimeSpan> ttl)
            {
                Events.Debugging($"Before GetTokenAsync for device {this.identity.Id}.");
                try
                {
                    string token = await this.cachedToken
                        .Filter(tk => IsTokenUsable(this.identity.IotHubHostName, tk))
                        .Map(tk => Task.FromResult(tk))
                        .GetOrElse(() => GetTokenFromCredentialsCacheAsync(this.credentialsCache, this.identity));
                    this.cachedToken = Option.Some(token);
                    this.firstFailureTime = Option.None<DateTime>();
                    Events.Debugging($"After GetTokenAsync for device {this.identity.Id}.");
                    return token;
                }
                catch (AuthenticationException ex)
                {
                    var timestamp = DateTime.Now;
                    if (this.firstFailureTime.Filter(fft => timestamp > fft + PollBuffer).HasValue)
                    {
                        // if firstFailureTime is post poll buffer
                        Events.Debugging($"After GetTokenAsync for device {this.identity.Id} failed with error: {ex}.");
                        Events.ErrorRenewingToken(ex);
                        throw;
                    }
                    else
                    {
                        // Convert to timeout exception to make DeviceClient retry
                        this.firstFailureTime = Option.Some(this.firstFailureTime.GetOrElse(timestamp));
                        Events.Debugging($"GetTokenAsync for device {this.identity.Id} temporary failed with error: {ex}.");
                        throw new TimeoutException(ex.Message);
                    }
                }
            }

            static async Task<string> GetTokenFromCredentialsCacheAsync(ICredentialsCache credentialsCache, IIdentity identity)
            {
                Events.Debugging($"Before get new token for device {identity} from ICredentialsCache.");
                var credentials = await credentialsCache.Get(identity);
                var token = credentials.Map(cr => cr as ITokenCredentials)
                    .Filter(tcr => IsTokenUsable(identity.IotHubHostName, tcr.Token))
                    .Map(tcr => tcr.Token)
                    .Expect(() => new AuthenticationException($"Unabled to get valid token from credentials cache for device {identity.Id}."));
                Events.Debugging($"After get new token for device {identity} from ICredentialsCache.");
                return token;
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
                Debugging
            }

            public static void Debugging(string message)
            {
                Log.LogError((int)EventIds.Debugging, $"[Debugging]-[ClientTokenCloudConnection]: {message}");
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
