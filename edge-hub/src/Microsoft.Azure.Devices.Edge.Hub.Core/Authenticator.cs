// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class Authenticator : IAuthenticator
    {
        readonly string edgeDeviceId;
        readonly IAuthenticator tokenAuthenticator;
        readonly ICredentialsStore credentialsCache;

        public Authenticator(IAuthenticator tokenAuthenticator, string edgeDeviceId, ICredentialsStore credentialsCache)
        {
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.tokenAuthenticator = Preconditions.CheckNotNull(tokenAuthenticator, nameof(tokenAuthenticator));
        }

        /// <summary>
        /// Authenticates the client credentials and adds it to connection manager if authenticated.
        /// </summary>
        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            Preconditions.CheckNotNull(clientCredentials);

            bool isAuthenticated = false;
            // If 'identity' represents an Edge module then its device id MUST match the authenticator's
            // 'edgeDeviceId'. In other words the authenticator for one edge device cannot authenticate
            // modules belonging to a different edge device.
            if (clientCredentials.Identity is IModuleIdentity moduleIdentity && !moduleIdentity.DeviceId.Equals(this.edgeDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Events.InvalidDeviceId(moduleIdentity, this.edgeDeviceId);
                isAuthenticated =false;
            }

            if (clientCredentials.AuthenticationType == AuthenticationType.X509Cert)
            {
                // If we reach here, we have validated the client cert. Validation is done in
                // DeviceIdentityProvider.cs::RemoteCertificateValidationCallback. In the future, we could
                // do authentication based on the CN. However, EdgeHub does not have enough information
                // currently to do CN validation.
                isAuthenticated = true;
            }
            else
            {
                isAuthenticated = await this.tokenAuthenticator.AuthenticateAsync(clientCredentials);
            }

            if (isAuthenticated)
            {
                await this.credentialsCache.Add(clientCredentials);
            }

            return isAuthenticated;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Authenticator>();
            const int IdStart = HubCoreEventIds.Authenticator;

            enum EventIds
            {
                AuthSuccess = IdStart,
                AuthError,
                InvalidDeviceError
            }

            public static void InvalidDeviceId(IModuleIdentity moduleIdentity, string edgeDeviceId)
            {
                Log.LogError((int)EventIds.InvalidDeviceError, Invariant($"Device Id {moduleIdentity.DeviceId} of module {moduleIdentity.ModuleId} is different from the edge device Id {edgeDeviceId}"));
            }

            public static void AuthResult(Try<ICloudProxy> cloudProxyTry, string id)
            {
                if (cloudProxyTry.Success)
                {
                    if (cloudProxyTry.Value.IsActive)
                    {
                        Log.LogInformation((int)EventIds.AuthSuccess, Invariant($"Successfully authenticated device {id}"));
                    }
                    else
                    {
                        Log.LogInformation((int)EventIds.AuthError, Invariant($"Unable to authenticate device {id}"));
                    }
                }
                else
                {
                    Log.LogError((int)EventIds.AuthError, cloudProxyTry.Exception, Invariant($"Unable to authenticate device {id}"));
                }
            }
        }
    }
}
