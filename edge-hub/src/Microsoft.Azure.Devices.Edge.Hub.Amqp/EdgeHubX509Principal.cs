// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using Microsoft.Azure.Amqp.X509;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using System.Threading.Tasks;
    using static System.FormattableString;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;


    class EdgeHubX509Principal : X509Principal, IAmqpAuthenticator
    {
        IList<X509Certificate2> chainCertificates;
        readonly string iotHubHostName;
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeHubX509Principal(X509CertificateIdentity identity,
                                    IList<X509Certificate2> chainCertificates,
                                    string iotHubHostName,
                                    IAuthenticator authenticator,
                                    IClientCredentialsFactory clientCredentialsProvider)
            : base(identity)
        {
            this.chainCertificates = Preconditions.CheckNotNull(chainCertificates, nameof(chainCertificates));
            this.iotHubHostName = Preconditions.CheckNotNull(iotHubHostName, nameof(iotHubHostName));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
        }

        public async Task<bool> AuthenticateAsync(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));

            (bool parseResult, string deviceId, string moduleId) identity = ParseIdentity(id);

            if (!identity.parseResult)
            {
                Events.AuthenticationFailed($"Id format invalid {identity}. Id expected to contain either deviceId or deviceId/moduleId");
                return false;
            }
            var clientCredentials = this.clientCredentialsProvider.GetWithX509Cert(identity.deviceId, identity.moduleId, string.Empty, this.CertificateIdentity.Certificate, this.chainCertificates);

            bool result = await this.authenticator.AuthenticateAsync(clientCredentials);
            if (!result)
            {
                Events.AuthenticationFailed($"Unable to authenticate device with Id {id}");
            }
            else
            {
                Events.AuthenticationSucceeded(id);
            }

            return result;
        }

        internal static (bool result, string deviceId, string moduleId) ParseIdentity(string identity)
        {
            string[] clientIdParts = identity.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if ((clientIdParts.Length == 0) || (clientIdParts.Length > 2))
            {
                return (false, string.Empty, string.Empty);
            }
            string deviceId = clientIdParts[0];
            string moduleId = clientIdParts.Length == 2 ? clientIdParts[0] : string.Empty;

            return (true, deviceId, moduleId);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubX509Principal>();
            const int IdStart = AmqpEventIds.X509PrinciparAuthenticator;

            enum EventIds
            {
                AuthenticationFailed = IdStart,
                AuthenticationSuccess
            }

            public static void AuthenticationFailed(string message, Exception ex = null)
            {
                if (ex == null)
                {
                    Log.LogDebug((int)EventIds.AuthenticationFailed, Invariant($"Amqp X.509 authentication failed due to following issue - {message}"));
                }
                else
                {
                    Log.LogWarning((int)EventIds.AuthenticationFailed, ex, Invariant($"Amqp X.509 authentication failed due to following issue - {message}"));
                }
            }

            public static void AuthenticationSucceeded(string id) =>
                Log.LogDebug((int)EventIds.AuthenticationSuccess, Invariant($"Amqp X.509 authentication succeeded for client with Id {id}"));
        }
    }
}
