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

            (bool isAuthenticated, bool shouldFallback) = await this.AuthenticateInternalAsync(tCredentials, false);
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

            (bool isAuthenticated, bool shouldFallback) = await this.AuthenticateInternalAsync(tCredentials, true);
            Events.ReauthenticatedInScope(clientCredentials.Identity, isAuthenticated);
            if (!isAuthenticated && shouldFallback)
            {
                Events.ServiceIdentityNotFound(tCredentials.Identity);
                isAuthenticated = await this.underlyingAuthenticator.ReauthenticateAsync(clientCredentials);
            }

            return isAuthenticated;
        }

        protected abstract bool AreInputCredentialsValid(T credentials);

        protected abstract bool ValidateWithServiceIdentity(ServiceIdentity serviceIdentity, T credentials);

        async Task<(bool isAuthenticated, bool shouldFallback)> AuthenticateInternalAsync(T tCredentials, bool reauthenticating)
        {
            try
            {
                if (!this.AreInputCredentialsValid(tCredentials))
                {
                    Events.InputCredentialsNotValid(tCredentials.Identity);
                    return (false, false);
                }

                bool syncServiceIdentity = this.syncServiceIdentityOnFailure && !reauthenticating;
                (bool isAuthenticated, bool valueFound) = await this.AuthenticateWithServiceIdentity(tCredentials, tCredentials.Identity.Id, syncServiceIdentity);
                if (!isAuthenticated && this.allowDeviceAuthForModule && tCredentials.Identity is IModuleIdentity moduleIdentity)
                {
                    // Module can use the Device key to authenticate
                    Events.AuthenticatingWithDeviceIdentity(moduleIdentity);
                    (isAuthenticated, valueFound) = await this.AuthenticateWithServiceIdentity(tCredentials, moduleIdentity.DeviceId, syncServiceIdentity);
                }

                // In the return value, the first flag indicates if the authentication succeeded.
                // The second flag indicates whether the authenticator should fall back to the underlying authenticator. This is done if
                // the ServiceIdentity was not found (which means the device/module is not in scope).
                return (isAuthenticated, !valueFound);
            }
            catch (Exception e)
            {
                Events.ErrorAuthenticating(e, tCredentials, reauthenticating);
                return (false, true);
            }
        }

        async Task<(bool isAuthenticated, bool serviceIdentityFound)> AuthenticateWithServiceIdentity(T credentials, string serviceIdentityId, bool syncServiceIdentity)
        {
            Option<ServiceIdentity> serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(serviceIdentityId);
            (bool isAuthenticated, bool serviceIdentityFound) = serviceIdentity.Map(s => (this.ValidateWithServiceIdentity(s, credentials), true)).GetOrElse((false, false));

            if (!isAuthenticated && (!serviceIdentityFound || syncServiceIdentity))
            {
                Events.ResyncingServiceIdentity(credentials.Identity, serviceIdentityId);
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
                ErrorAuthenticating = IdStart,
                ServiceIdentityNotFound,
                AuthenticatedInScope,
                InputCredentialsNotValid,
                ResyncingServiceIdentity,
                AuthenticatingWithDeviceIdentity
            }

            public static void ErrorAuthenticating(Exception exception, IClientCredentials credentials, bool reauthenticating)
            {
                string operation = reauthenticating ? "reauthenticating" : "authenticating";
                Log.LogWarning((int)EventIds.ErrorAuthenticating, exception, $"Error {operation} credentials for {credentials.Identity.Id}");
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

            public static void InputCredentialsNotValid(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.InputCredentialsNotValid, $"Credentials for client {identity.Id} are not valid.");
            }

            public static void ResyncingServiceIdentity(IIdentity identity, string serviceIdentityId)
            {
                Log.LogInformation((int)EventIds.ResyncingServiceIdentity, $"Unable to authenticate client {identity.Id} with cached service identity {serviceIdentityId}. Resyncing service identity...");
            }

            public static void AuthenticatingWithDeviceIdentity(IModuleIdentity moduleIdentity)
            {
                Log.LogInformation((int)EventIds.AuthenticatingWithDeviceIdentity, $"Unable to authenticate client {moduleIdentity.Id} with module credentials. Attempting to authenticate using device {moduleIdentity.DeviceId} credentials.");
            }
        }
    }
}
