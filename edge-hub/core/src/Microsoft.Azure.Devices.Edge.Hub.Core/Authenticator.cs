// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
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
            else
            {
                Metrics.AddAuthenticationFailure(clientCredentials.Identity.Id);
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
                string operation = reAuthenticating ? "re-authentication" : "authentication";
                if (result)
                {
                    Log.LogDebug((int)EventIds.AuthResult, Invariant($"Client {clientCredentials.Identity.Id} {operation} successful"));
                }
                else
                {
                    Log.LogDebug((int)EventIds.AuthResult, Invariant($"{clientCredentials.Identity.Id} {operation} failure"));
                }
            }
        }

        static class Metrics
        {
            static readonly IMetricsCounter AuthCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                    "client_connect_failed",
                    "Client connection failure",
                    new List<string> { "id", "reason", MetricsConstants.MsTelemetry });

            public static void AddAuthenticationFailure(string id) => AuthCounter.Increment(1, new[] { id, "not_authenticated", bool.TrueString });
        }
    }
}
