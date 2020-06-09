// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp.X509;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class EdgeX509Principal : X509Principal, IAmqpAuthenticator
    {
        readonly IList<X509Certificate2> chainCertificates;
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeX509Principal(
            X509CertificateIdentity identity,
            IList<X509Certificate2> chainCertificates,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(identity)
        {
            this.chainCertificates = Preconditions.CheckNotNull(chainCertificates, nameof(chainCertificates));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
        }

        public async Task<bool> AuthenticateAsync(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            (bool parseResult, string deviceId, string moduleId) identity = ParseIdentity(id);

            if (!identity.parseResult)
            {
                Events.IdentityParseFailed(id);
                return false;
            }

            IClientCredentials clientCredentials = this.clientCredentialsProvider.GetWithX509Cert(identity.deviceId, identity.moduleId, string.Empty, this.CertificateIdentity.Certificate, this.chainCertificates);

            bool result = await this.authenticator.AuthenticateAsync(clientCredentials);
            if (!result)
            {
                Events.AuthenticationFailed(id);
            }
            else
            {
                Events.AuthenticationSucceeded(id);
            }

            return result;
        }

        internal static (bool result, string deviceId, string moduleId) ParseIdentity(string identity)
        {
            string[] clientIdParts = identity.Split('/');
            if ((clientIdParts.Length == 0) || (clientIdParts.Length > 2))
            {
                return (false, string.Empty, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(clientIdParts[0]))
            {
                return (false, string.Empty, string.Empty);
            }

            string deviceId = clientIdParts[0];
            string moduleId = string.Empty;
            if (clientIdParts.Length == 2)
            {
                if (string.IsNullOrWhiteSpace(clientIdParts[1]))
                {
                    return (false, string.Empty, string.Empty);
                }
                else
                {
                    moduleId = clientIdParts[1];
                }
            }

            return (true, deviceId, moduleId);
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.X509PrinciparAuthenticator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeX509Principal>();

            enum EventIds
            {
                AuthenticationFailed = IdStart,
                IdentityParseFailed,
                AuthenticationSuccess
            }

            public static void IdentityParseFailed(string id, Exception ex = null) =>
                Log.LogWarning((int)EventIds.IdentityParseFailed, Invariant($"Amqp X.509 authentication failed. Id format invalid {id}. Id expected to contain either deviceId or deviceId/moduleId"));

            public static void AuthenticationFailed(string id, Exception ex = null) =>
                Log.LogWarning((int)EventIds.AuthenticationFailed, ex, Invariant($"Unable to authenticate device with Id {id}"));

            public static void AuthenticationSucceeded(string id) =>
                Log.LogDebug((int)EventIds.AuthenticationSuccess, Invariant($"Amqp X.509 authentication succeeded for client with Id {id}"));
        }
    }
}
