// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TokenCacheAuthenticator : IAuthenticator
    {
        readonly IAuthenticator cloudAuthenticator;
        readonly ICredentialsCache credentialsCache;
        readonly string iotHubHostName;

        public TokenCacheAuthenticator(IAuthenticator cloudAuthenticator, ICredentialsCache credentialsCache, string iotHubHostName)
        {
            this.cloudAuthenticator = Preconditions.CheckNotNull(cloudAuthenticator, nameof(cloudAuthenticator));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        public Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, false);

        public Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, true);

        async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials, bool reauthenticating)
        {
            if (!(clientCredentials is ITokenCredentials tokenCredentials))
            {
                return false;
            }

            Option<IClientCredentials> validatedCredentials = await this.credentialsCache.Get(tokenCredentials.Identity);
            bool isAuthenticated = await validatedCredentials.Map(
                    v => Task.FromResult(v is ITokenCredentials validatedTokenCredentials &&
                        this.IsValid(clientCredentials, validatedTokenCredentials.Token) &&
                        validatedTokenCredentials.Token.Equals(tokenCredentials.Token)))
                .GetOrElse(Task.FromResult(false));

            if (isAuthenticated)
            {
                Events.AuthenticatedFromCache(clientCredentials.Identity);
            }
            else
            {
                isAuthenticated = await (reauthenticating
                    ? this.cloudAuthenticator.ReauthenticateAsync(clientCredentials)
                    : this.cloudAuthenticator.AuthenticateAsync(clientCredentials));

                if (isAuthenticated)
                {
                    await this.credentialsCache.Add(clientCredentials);
                }
            }

            return isAuthenticated;
        }

        bool IsValid(IClientCredentials clientCredentials, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubHostName, token);
                DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                return expiryTime > DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Events.ErrorValidatingCachedToken(clientCredentials.Identity, e);
                return false;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TokenCacheAuthenticator>();
            const int IdStart = CloudProxyEventIds.TokenCredentialsAuthenticator;

            enum EventIds
            {
                AuthenticatedFromCache = IdStart,
                AuthenticatedWithCloud,
                ErrorValidatingCachedToken
            }

            public static void AuthenticatedFromCache(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticatedFromCache, $"Authenticated {identity.Id} from the cached token");
            }

            public static void AuthenticatedWithIotHub(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticatedWithCloud, $"Authenticated {identity.Id} with IotHub");
            }

            public static void ErrorValidatingCachedToken(IIdentity identity, Exception exception)
            {
                Log.LogDebug((int)EventIds.ErrorValidatingCachedToken, $"Error validating cached token for {identity.Id}: {exception.Message}");
            }
        }
    }
}
