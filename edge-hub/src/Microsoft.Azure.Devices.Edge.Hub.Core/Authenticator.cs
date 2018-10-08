// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    using static System.FormattableString;

    public class Authenticator : IAuthenticator
    {
        readonly ICredentialsCache credentialsCache;

        readonly string edgeDeviceId;

        readonly IAuthenticator tokenAuthenticator;

        public Authenticator(IAuthenticator tokenAuthenticator, string edgeDeviceId, ICredentialsCache credentialsCache)
        {
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.tokenAuthenticator = Preconditions.CheckNotNull(tokenAuthenticator, nameof(tokenAuthenticator));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(ICredentialsCache));
        }

        public Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, false);

        public Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, true);

        async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials, bool reAuthenticating)
        {
            Preconditions.CheckNotNull(clientCredentials);

            bool isAuthenticated;
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
                isAuthenticated = await (reAuthenticating
                    ? this.tokenAuthenticator.ReauthenticateAsync(clientCredentials)
                    : this.tokenAuthenticator.AuthenticateAsync(clientCredentials));
            }

            if (isAuthenticated)
            {
                await this.credentialsCache.Add(clientCredentials);
            }

            Events.AuthResult(clientCredentials, reAuthenticating, isAuthenticated);
            return isAuthenticated;
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.Authenticator;

            static readonly ILogger Log = Logger.Factory.CreateLogger<Authenticator>();

            enum EventIds
            {
                AuthResult = IdStart,

                InvalidDeviceError
            }

            public static void AuthResult(IClientCredentials clientCredentials, bool reAuthenticating, bool result)
            {
                string operation = reAuthenticating ? "re-authenticated" : "authenticated";
                if (result)
                {
                    Log.LogDebug((int)EventIds.AuthResult, Invariant($"Client {clientCredentials.Identity.Id} {operation} successfully"));
                }
                else
                {
                    Log.LogDebug((int)EventIds.AuthResult, Invariant($"Unable to {operation} client {clientCredentials.Identity.Id}"));
                }
            }

            public static void InvalidDeviceId(IModuleIdentity moduleIdentity, string edgeDeviceId)
            {
                Log.LogError((int)EventIds.InvalidDeviceError, Invariant($"Device Id {moduleIdentity.DeviceId} of module {moduleIdentity.ModuleId} is different from the edge device Id {edgeDeviceId}"));
            }
        }
    }
}
