// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
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
            if (clientCredentials.AuthenticationType == AuthenticationType.Implicit)
            {
                // Implicit authentication is executed when in a nested scenario a parent edge device captures a
                // an IotHub message on an mqtt broker topic belonging to a device never seen before. In this case the
                // child edge device has authenticated the connecting device, the authorization is continously monitoring
                // if the device is publishing on allowed topics, so when a message arrives on a topic belonging to
                // the device, it is sure that it has been authenticated/authorized before. Now just create an entry
                // for it without further checks
                isAuthenticated = true;
            }
            else if (clientCredentials.AuthenticationType == AuthenticationType.X509Cert)
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
                try
                {
                    await this.credentialsCache.Add(clientCredentials);
                }
                catch (Exception ex)
                {
                    // Authenticated but failed to add to cred cache,
                    // this will eventually cause a re-auth error but
                    // there's nothing we can do here
                    Events.CredentialsCacheFailure(ex);
                }
            }
            else if (!reAuthenticating)
            {
                // only report authentication failure on initial authentication
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
                InvalidDeviceError,
                CredentialsCacheFailure,
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

            public static void CredentialsCacheFailure(Exception ex)
            {
                Log.LogError((int)EventIds.CredentialsCacheFailure, Invariant($"Credentials cache failed with exception {ex.GetType()}:{ex.Message}"));
            }
        }

        static class Metrics
        {
            static readonly IMetricsCounter AuthCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                    "client_connect_failed",
                    "Total number of times each client failed to connect to edgeHub",
                    new List<string> { "id", "reason", MetricsConstants.MsTelemetry });

            public static void AddAuthenticationFailure(string id) => AuthCounter.Increment(1, new[] { id, "not_authenticated", bool.TrueString });
        }
    }
}
