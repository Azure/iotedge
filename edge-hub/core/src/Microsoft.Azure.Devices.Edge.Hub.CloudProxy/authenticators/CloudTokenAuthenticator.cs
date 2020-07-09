// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudTokenAuthenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;
        readonly string iotHubHostName;

        public CloudTokenAuthenticator(IConnectionManager connectionManager, string iotHubHostName)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials))
            {
                return false;
            }

            Try<ICloudProxy> cloudProxyTry = await this.connectionManager.CreateCloudConnectionAsync(clientCredentials);
            if (cloudProxyTry.Success)
            {
                Events.AuthenticatedWithIotHub(clientCredentials.Identity);
                return true;
            }
            else
            {
                Events.ErrorValidatingTokenWithIoTHub(clientCredentials.Identity, cloudProxyTry.Exception);
            }

            return false;
        }

        public Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials tokenCredentials))
            {
                return Task.FromResult(false);
            }

            // Only check if the token is expired.
            bool isAuthenticated = this.TryGetSharedAccessSignature(tokenCredentials.Token, clientCredentials.Identity, out SharedAccessSignature sharedAccessSignature) &&
                !sharedAccessSignature.IsExpired();

            Events.ReauthResult(clientCredentials, isAuthenticated);
            return Task.FromResult(isAuthenticated);
        }

        bool TryGetSharedAccessSignature(string token, IIdentity identity, out SharedAccessSignature sharedAccessSignature)
        {
            try
            {
                sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubHostName, token);
                return true;
            }
            catch (Exception e)
            {
                Events.ErrorParsingToken(identity, e);
                sharedAccessSignature = null;
                return false;
            }
        }

        static class Events
        {
            const int IdStart = CloudProxyEventIds.CloudTokenAuthenticator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudTokenAuthenticator>();

            enum EventIds
            {
                AuthenticatedWithCloud = IdStart,
                ErrorValidatingToken,
                ErrorGettingCloudProxy,
                ErrorParsingToken
            }

            public static void AuthenticatedWithIotHub(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.AuthenticatedWithCloud, $"Authenticated {identity.Id} with IotHub");
            }

            public static void ErrorValidatingTokenWithIoTHub(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorValidatingToken, ex, $"Error validating token for {identity.Id} with IoTHub");
            }

            public static void ErrorGettingCloudProxy(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingCloudProxy, ex, $"Error getting cloud proxy for {identity.Id}");
            }

            public static void ReauthResult(IClientCredentials clientCredentials, bool isAuthenticated)
            {
                string operation = isAuthenticated ? "succeeded" : "failed";
                Log.LogDebug((int)EventIds.AuthenticatedWithCloud, $"Reauthenticating {clientCredentials.Identity.Id} with IotHub {operation}");
            }

            public static void ErrorParsingToken(IIdentity identity, Exception exception)
            {
                Log.LogDebug((int)EventIds.ErrorParsingToken, exception, $"Error parsing token for client {identity.Id} while re-authenticating");
            }
        }
    }
}
