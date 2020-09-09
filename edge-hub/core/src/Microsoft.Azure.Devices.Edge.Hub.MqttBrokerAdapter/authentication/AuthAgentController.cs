// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class AuthAgentController : Controller
    {
        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly ISystemComponentIdProvider systemComponentIdProvider;

        public AuthAgentController(IAuthenticator authenticator, IUsernameParser usernameParser, IClientCredentialsFactory clientCredentialsFactory, ISystemComponentIdProvider systemComponentIdProvider)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider, nameof(systemComponentIdProvider));
        }

        [HttpPost]
        [Produces("application/json")]
        public async Task<JsonResult> HandleAsync([FromBody] AuthRequest request)
        {
            if (request == null)
            {
                Events.ErrorDecodingPayload();
                return this.Json(GetErrorResult());
            }

            if (!string.Equals(request.Version, AuthAgentConstants.ApiVersion))
            {
                Events.ErrorBadVersion(request.Version ?? "(none)");
                return this.Json(GetErrorResult());
            }

            try
            {
                var isAuthenticated = default(bool);
                var credentials = default(Option<IClientCredentials>);

                (isAuthenticated, credentials) =
                    await this.GetIdsFromUsername(request)
                                .FlatMap(ci => this.GetCredentials(request, ci))
                                .Match(
                                    async creds => (await this.AuthenticateAsync(creds), Option.Some(creds)),
                                    () => Task.FromResult((false, Option.None<IClientCredentials>())));

                return this.Json(GetAuthResult(isAuthenticated, credentials));
            }
            catch (Exception e)
            {
                Events.ErrorProcessingRequest(e);
                return this.Json(GetErrorResult());
            }
        }

        Option<ClientInfo> GetIdsFromUsername(AuthRequest request)
        {
            try
            {
                return Option.Some(this.usernameParser.Parse(request.Username));
            }
            catch (Exception e)
            {
                Events.InvalidUsernameFormat(e);
                return Option.None<ClientInfo>();
            }
        }

        Option<IClientCredentials> GetCredentials(AuthRequest request, ClientInfo clientInfo)
        {
            var result = Option.None<IClientCredentials>();

            try
            {
                var isPasswordPresent = !string.IsNullOrWhiteSpace(request.Password);
                var isCertificatePresent = !string.IsNullOrWhiteSpace(request.EncodedCertificate);

                if (isPasswordPresent && isCertificatePresent)
                {
                    Events.MoreCredentialsSpecified();
                }
                else if (isPasswordPresent)
                {
                    result = Option.Some(
                                this.clientCredentialsFactory.GetWithSasToken(
                                                                    clientInfo.DeviceId,
                                                                    clientInfo.ModuleId,
                                                                    clientInfo.DeviceClientType,
                                                                    request.Password,
                                                                    false,
                                                                    clientInfo.ModelId));
                }
                else if (isCertificatePresent)
                {
                    var certificate = DecodeCertificate(request.EncodedCertificate);
                    var chain = DecodeCertificateChain(request.EncodedCertificateChain);

                    result = Option.Some(
                                this.clientCredentialsFactory.GetWithX509Cert(
                                                                    clientInfo.DeviceId,
                                                                    clientInfo.ModuleId,
                                                                    clientInfo.DeviceClientType,
                                                                    certificate,
                                                                    chain,
                                                                    clientInfo.ModelId));
                }
                else
                {
                    Events.NoCredentialsSpecified();
                }
            }
            catch (Exception e)
            {
                Events.InvalidCredentials(e);
            }

            return result;
        }

        async Task<bool> AuthenticateAsync(IClientCredentials credentials)
        {
            try
            {
                return await this.authenticator.AuthenticateAsync(credentials);
            }
            catch (Exception e)
            {
                Events.InvalidCredentials(e);
            }

            return false;
        }

        static object GetAuthResult(bool isAuthenticated, Option<IClientCredentials> credentials)
        {
            // note, that if authenticated, then these values are present, and defaults never apply
            var id = credentials.Map(c => c.Identity.Id).GetOrElse("anonymous");

            if (isAuthenticated)
            {
                Events.AuthSucceeded(id);
                return new
                {
                    result = AuthAgentConstants.Authenticated,
                    identity = id,
                    version = AuthAgentConstants.ApiVersion
                };
            }
            else
            {
                Events.AuthFailed(id);
                return GetErrorResult();
            }
        }

        static object GetErrorResult() => new { result = AuthAgentConstants.Unauthenticated, version = AuthAgentConstants.ApiVersion };

        static X509Certificate2 DecodeCertificate(string encodedCertificate)
        {
            try
            {
                var certificateContent = Encoding.UTF8.GetBytes(encodedCertificate);
                var certificate = new X509Certificate2(certificateContent);

                return certificate;
            }
            catch (Exception e)
            {
                Events.ErrorDecodingCertificate(e);
                throw;
            }
        }

        static List<X509Certificate2> DecodeCertificateChain(string[] encodedCertificates)
        {
            if (encodedCertificates != null)
            {
                return encodedCertificates.Select(DecodeCertificate).ToList();
            }
            else
            {
                return new List<X509Certificate2>();
            }
        }

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentController>();

            enum EventIds
            {
                AuthSucceeded,
                AuthFailed,
                ErrorDecodingPayload,
                ErrorDecodingCertificate,
                ErrorProcessingRequest,
                ErrorBadVersion,
                NoCredentialsSpecified,
                MoreCredentialsSpecified,
                InvalidUsernameFormat,
                InvalidCredentials,
            }

            public static void AuthSucceeded(string id) => Log.LogInformation((int)EventIds.AuthSucceeded, "AUTH succeeded {0}", id);
            public static void AuthFailed(string id) => Log.LogWarning((int)EventIds.AuthFailed, "AUTH failed {0}", id);
            public static void ErrorDecodingPayload() => Log.LogWarning((int)EventIds.ErrorDecodingPayload, "Error decoding AUTH request, invalid JSON structure");
            public static void ErrorDecodingCertificate(Exception e) => Log.LogWarning((int)EventIds.ErrorDecodingCertificate, e, "Error decoding certificate");
            public static void ErrorProcessingRequest(Exception e) => Log.LogWarning((int)EventIds.ErrorProcessingRequest, e, "Error processing AUTH request");
            public static void ErrorBadVersion(string version) => Log.LogWarning((int)EventIds.ErrorProcessingRequest, "Bad version number received with AUTH request {0}", version);
            public static void NoCredentialsSpecified() => Log.LogWarning((int)EventIds.NoCredentialsSpecified, "No credentials specified: either a certificate or a SAS token must be present for AUTH");
            public static void MoreCredentialsSpecified() => Log.LogWarning((int)EventIds.MoreCredentialsSpecified, "More credentials specified: only a certificate or a SAS token must be present for AUTH");
            public static void InvalidUsernameFormat(Exception e) => Log.LogWarning((int)EventIds.InvalidUsernameFormat, e, "Invalid username format provided for AUTH");
            public static void InvalidCredentials(Exception e) => Log.LogWarning((int)EventIds.InvalidCredentials, e, "Invalid credentials provided for AUTH");
        }
    }
}
