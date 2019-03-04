// Copyright (c) Microsoft. All rights reserved.
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
        readonly IAuthenticator tokenAuthenticator;
        readonly IAuthenticator certificateAuthenticator;
        readonly ICredentialsCache credentialsCache;

        public Authenticator(IAuthenticator tokenAuthenticator, IAuthenticator certificateAuthenticator, ICredentialsCache credentialsCache)
        {
            this.tokenAuthenticator = Preconditions.CheckNotNull(tokenAuthenticator, nameof(tokenAuthenticator));
            this.certificateAuthenticator = Preconditions.CheckNotNull(certificateAuthenticator, nameof(certificateAuthenticator));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(ICredentialsCache));
        }

        /// <summary>
        /// Authenticates the client credentials
        /// </summary>
        public Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, false);

        /// <summary>
        /// Reauthenticates the client credentials
        /// </summary>
        public Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, true);

        async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials, bool reAuthenticating)
        {
            Preconditions.CheckNotNull(clientCredentials);

            bool isAuthenticated;
            if (clientCredentials.AuthenticationType == AuthenticationType.X509Cert)
            {
                isAuthenticated = await (reAuthenticating
                    ? this.certificateAuthenticator.ReauthenticateAsync(clientCredentials)
                    : this.certificateAuthenticator.AuthenticateAsync(clientCredentials));
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

            public static void InvalidDeviceId(IModuleIdentity moduleIdentity, string edgeDeviceId)
            {
                Log.LogError((int)EventIds.InvalidDeviceError, Invariant($"Device Id {moduleIdentity.DeviceId} of module {moduleIdentity.ModuleId} is different from the edge device Id {edgeDeviceId}"));
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
        }
    }
}
