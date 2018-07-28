// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TokenCredentialsAuthenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;
        readonly ICredentialsStore credentialsStore;
        readonly string iotHubHostName;

        public TokenCredentialsAuthenticator(IConnectionManager connectionManager, ICredentialsStore credentialsStore, string iotHubHostName)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.credentialsStore = Preconditions.CheckNotNull(credentialsStore, nameof(credentialsStore));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials tokenCredentials))
            {
                return false;
            }

            Option<IClientCredentials> validatedCredentials = await this.credentialsStore.Get(tokenCredentials.Identity);
            bool isAuthenticated = await validatedCredentials.Map(
                v => Task.FromResult(v is ITokenCredentials validatedTokenCredentials &&
                    this.IsValid(clientCredentials, validatedTokenCredentials.Token) &&
                    validatedTokenCredentials.Token.Equals(tokenCredentials.Token)))
                .GetOrElse(Task.FromResult(false));

            if (!isAuthenticated)
            {
                Try<ICloudProxy> cloudProxyTry = await this.connectionManager.CreateCloudConnectionAsync(clientCredentials);
                if (cloudProxyTry.Success)
                {
                    try
                    {
                        await cloudProxyTry.Value.OpenAsync();
                        isAuthenticated = true;
                        await this.credentialsStore.Add(tokenCredentials);
                        Events.AuthenticatedWithIotHub(clientCredentials.Identity);
                    }
                    catch (Exception ex)
                    {
                        Events.AuthenticatingWithIotHubFailed(clientCredentials.Identity, ex);
                        isAuthenticated = false;
                    }
                }
            }
            else
            {
                Events.AuthenticatedFromCache(clientCredentials.Identity);
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<TokenCredentialsAuthenticator>();
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

            public static void AuthenticatingWithIotHubFailed(IIdentity identity, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorValidatingCachedToken, $"Error validating token with IoTHub {identity.Id}: {ex.Message}");
            }
        }
    }
}
