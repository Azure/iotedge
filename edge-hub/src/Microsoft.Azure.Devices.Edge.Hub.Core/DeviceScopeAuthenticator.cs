// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class DeviceScopeAuthenticator<T> : IAuthenticator
        where T : IClientCredentials
    {
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly IAuthenticator underlyingAuthenticator;
        readonly bool allowDeviceAuthForModule;
        readonly bool syncServiceIdentityOnFailure;

        protected DeviceScopeAuthenticator(
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            IAuthenticator underlyingAuthenticator,
            bool allowDeviceAuthForModule,
            bool syncServiceIdentityOnFailure)
        {
            this.underlyingAuthenticator = Preconditions.CheckNotNull(underlyingAuthenticator, nameof(underlyingAuthenticator));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.allowDeviceAuthForModule = allowDeviceAuthForModule;
            this.syncServiceIdentityOnFailure = syncServiceIdentityOnFailure;
        }

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is T tCredentials))
            {
                return false;
            }

            (bool isAuthenticated, bool shouldFallback) = await this.AuthenticateInternalAsync(tCredentials);
            Events.AuthenticatedInScope(clientCredentials.Identity, isAuthenticated);
            if (!isAuthenticated && shouldFallback)
            {
                isAuthenticated = await this.underlyingAuthenticator.AuthenticateAsync(clientCredentials);
            }

            return isAuthenticated;
        }

        public async Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is T tCredentials))
            {
                return false;
            }

            (bool isAuthenticated, bool shouldFallback) = await this.AuthenticateInternalAsync(tCredentials);
            Events.ReauthenticatedInScope(clientCredentials.Identity, isAuthenticated);
            if (!isAuthenticated && shouldFallback)
            {
                isAuthenticated = await this.underlyingAuthenticator.ReauthenticateAsync(clientCredentials);
            }

            return isAuthenticated;
        }

        protected abstract bool AreInputCredentialsValid(T credentials);

        protected abstract bool ValidateWithServiceIdentity(ServiceIdentity serviceIdentity, T credentials);

        async Task<(bool isAuthenticated, bool shouldFallback)> AuthenticateInternalAsync(T tCredentials)
        {
            try
            {
                if (!this.AreInputCredentialsValid(tCredentials))
                {
                    return (false, false);
                }

                (bool isAuthenticated, bool valueFound) = await this.AuthenticateWithServiceIdentity(tCredentials, tCredentials.Identity.Id);
                if (!isAuthenticated && this.allowDeviceAuthForModule && tCredentials.Identity is IModuleIdentity moduleIdentity)
                {
                    // Module can use the Device key to authenticate
                    (isAuthenticated, valueFound) = await this.AuthenticateWithServiceIdentity(tCredentials, moduleIdentity.DeviceId);
                }

                return (isAuthenticated, !valueFound);
            }
            catch (Exception e)
            {
                Events.ErrorAuthenticating(e, tCredentials);
                return (false, true);
            }
        }

        async Task<(bool isAuthenticated, bool serviceIdentityFound)> AuthenticateWithServiceIdentity(T credentials, string serviceIdentityId)
        {
            Option<ServiceIdentity> serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(serviceIdentityId);
            (bool isAuthenticated, bool serviceIdentityFound) = serviceIdentity.Map(s => (this.ValidateWithServiceIdentity(s, credentials), true)).GetOrElse((false, false));

            if (!isAuthenticated && (!serviceIdentityFound || this.syncServiceIdentityOnFailure))
            {
                await this.deviceScopeIdentitiesCache.RefreshServiceIdentity(serviceIdentityId);
                serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(serviceIdentityId);
                (isAuthenticated, serviceIdentityFound) = serviceIdentity.Map(s => (this.ValidateWithServiceIdentity(s, credentials), true)).GetOrElse((false, false));
            }

            return (isAuthenticated, serviceIdentityFound);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeAuthenticator<T>>();
            const int IdStart = HubCoreEventIds.DeviceScopeAuthenticator;

            enum EventIds
            {
                ErrorReauthenticating = IdStart,
                InvalidHostName,
                InvalidAudience,
                IdMismatch,
                KeysMismatch,
                InvalidServiceIdentityType,
                ErrorAuthenticating,
                ServiceIdentityNotEnabled,
                TokenExpired,
                ErrorParsingToken,
                ServiceIdentityNotFound,
                AuthenticatedInScope
            }

            public static void ErrorReauthenticating(Exception exception, ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.ErrorReauthenticating, exception, $"Error re-authenticating {serviceIdentity.Id} after the service identity was updated.");
            }

            public static void InvalidHostName(string id, string hostName, string iotHubHostName, string edgeHubHostName)
            {
                Log.LogWarning((int)EventIds.InvalidHostName, $"Error authenticating token for {id} because the audience hostname {hostName} does not match IoTHub hostname {iotHubHostName} or the EdgeHub hostname {edgeHubHostName}.");
            }

            public static void InvalidAudience(string audience, IIdentity identity)
            {
                Log.LogWarning((int)EventIds.InvalidAudience, $"Error authenticating token for {identity.Id} because the audience {audience} is invalid.");
            }

            public static void IdMismatch(string audience, IIdentity identity, string deviceId)
            {
                Log.LogWarning((int)EventIds.IdMismatch, $"Error authenticating token for {identity.Id} because the deviceId {deviceId} in the identity does not match the audience {audience}.");
            }

            public static void KeysMismatch(string id, Exception exception)
            {
                Log.LogWarning((int)EventIds.KeysMismatch, $"Error authenticating token for {id} because the token did not match the primary or the secondary key. Error - {exception.Message}");
            }

            public static void InvalidServiceIdentityType(ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Error authenticating token for {serviceIdentity.Id} because the service identity authentication type is unexpected - {serviceIdentity.Authentication.Type}");
            }

            public static void ErrorAuthenticating(Exception exception, IClientCredentials credentials)
            {
                Log.LogWarning((int)EventIds.ErrorAuthenticating, exception, $"Error authenticating credentials for {credentials.Identity.Id}");
            }

            public static void ServiceIdentityNotEnabled(ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.ServiceIdentityNotEnabled, $"Error authenticating token for {serviceIdentity.Id} because the service identity is not enabled");
            }

            public static void TokenExpired(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.TokenExpired, $"Error authenticating token for {identity.Id} because the token has expired.");
            }

            public static void ErrorParsingToken(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorParsingToken, exception, $"Error authenticating token for {identity.Id} because the token could not be parsed");
            }

            public static void ServiceIdentityNotFound(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityNotFound, $"Service identity for {identity.Id} not found. Using underlying authenticator to authenticate");
            }

            public static void AuthenticatedInScope(IIdentity identity, bool isAuthenticated)
            {
                string authenticated = isAuthenticated ? "authenticated" : "not authenticated";
                Log.LogInformation((int)EventIds.AuthenticatedInScope, $"Client {identity.Id} in device scope {authenticated} locally.");
            }

            public static void ReauthenticatedInScope(IIdentity identity, bool isAuthenticated)
            {
                string authenticated = isAuthenticated ? "reauthenticated" : "not reauthenticated";
                Log.LogDebug((int)EventIds.AuthenticatedInScope, $"Client {identity.Id} in device scope {authenticated} locally.");
            }
        }
    }
}
