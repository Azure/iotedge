// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;

    using Newtonsoft.Json;

    public class AuthAgentRequestHandler
    {
        // The maximum allowed request size. The biggest item in a request is the certificate chain,
        // 32k for the certificates and other data should be more than enough. Bigger requests are
        // refused.
        const int MaxInBufferSize = 32 * 1024;
        const string Post = "POST";

        static readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(5);

        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly AuthAgentProtocolHeadConfig config;

        public AuthAgentRequestHandler(IAuthenticator authenticator, IUsernameParser usernameParser, IClientCredentialsFactory clientCredentialsFactory, AuthAgentProtocolHeadConfig config)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.config = Preconditions.CheckNotNull(config, nameof(config));
        }

        public static IWebHost CreateWebHostBuilder(
                                    IAuthenticator authenticator,
                                    IUsernameParser usernameParser,
                                    IClientCredentialsFactory clientCredentialsFactory,
                                    AuthAgentProtocolHeadConfig config)
        {
            return WebHost.CreateDefaultBuilder()
                          .UseStartup<AuthAgentRequestHandler>()
                          .UseUrls($"http://*:{config.Port}")
                          .ConfigureServices(s => s.TryAddSingleton(authenticator))
                          .ConfigureServices(s => s.TryAddSingleton(usernameParser))
                          .ConfigureServices(s => s.TryAddSingleton(clientCredentialsFactory))
                          .ConfigureServices(s => s.TryAddSingleton(config))
                          .ConfigureLogging(c => c.ClearProviders())
                          .Build();
        }

        public void Configure(IApplicationBuilder app) => app.Use(this.CallFilter).Run(this.Handler);

        async Task Handler(HttpContext context)
        {
            try
            {
                Events.AuthRequestReceived();

                var requestString = await ReadRequestBodyAsStringAsync(context.Request);
                var requestRecord = Decode(requestString);

                await requestRecord.Match(
                    async r => await this.SendResponseAsync(r, context),
                    async () => await SendErrorAsync(context));
            }
            catch (Exception e)
            {
                Events.ErrorHandlingRequest(e);
                context.Response.StatusCode = Convert.ToInt32(HttpStatusCode.BadRequest);
            }
        }

        async Task CallFilter(HttpContext context, Func<Task> next)
        {
            if (!IsPost(context.Request.Method) || !this.IsBaseUrl(context.Request.Path))
            {
                context.Response.StatusCode = Convert.ToInt32(HttpStatusCode.NotFound);
                return;
            }

            await next.Invoke();
        }

        async Task SendResponseAsync(AuthRequest request, HttpContext context)
        {
            try
            {
                var isAuthenticated = default(bool);
                var credentials = default(Option<IClientCredentials>);

                (isAuthenticated, credentials) =
                    await this.GetIdsFromUsername(request)
                                .FlatMap(ids => this.GetCredentials(request, ids.deviceId, ids.moduleId, ids.deviceClientType))
                                .Match(
                                    async creds => (await this.AuthenticateAsync(creds), Option.Some(creds)),
                                    () => Task.FromResult((false, Option.None<IClientCredentials>())));

                await SendAuthResultAsync(context, isAuthenticated, credentials);
            }
            catch (Exception e)
            {
                Events.ErrorSendingResponse(e);
                return;
            }
        }

        Option<(string deviceId, string moduleId, string deviceClientType)> GetIdsFromUsername(AuthRequest request)
        {
            try
            {
                return Option.Some(this.usernameParser.Parse(request.Username));
            }
            catch (Exception e)
            {
                Events.InvalidUsernameFormat(e);
                return Option.None<(string, string, string)>();
            }
        }

        Option<IClientCredentials> GetCredentials(AuthRequest request, string deviceId, string moduleId, string deviceClientType)
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
                    result = Option.Some(this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, request.Password, false));
                }
                else if (isCertificatePresent)
                {
                    var certificate = DecodeCertificate(request.EncodedCertificate);
                    var chain = DecodeCertificateChain(request.EncodedCertificateChain);

                    result = Option.Some(this.clientCredentialsFactory.GetWithX509Cert(deviceId, moduleId, deviceClientType, certificate, chain));
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

        bool IsBaseUrl(PathString path) => path.HasValue && string.Equals(path.Value, this.config.BaseUrl, StringComparison.Ordinal);

        static Task SendAuthResultAsync(HttpContext context, bool isAuthenticated, Option<IClientCredentials> credentials)
        {
            // note, that if authenticated, then these values are present, and defaults never apply
            var iotHubName = credentials.Map(c => c.Identity.IotHubHostName).GetOrElse("any");
            var id = credentials.Map(c => c.Identity.Id).GetOrElse("anonymous");

            if (isAuthenticated)
            {
                Events.AuthSucceeded(id);
                return SendAsync(context, $@"{{""result"":200,""identity"":""{iotHubName}/{id}"",""version"":""2020-04-20""}}");
            }
            else
            {
                Events.AuthFailed(id);
                return SendErrorAsync(context);
            }
        }

        static Task SendErrorAsync(HttpContext context)
        {
            // we don't reveal error details: for the caller if something is not right, that is unauthenticated
            return SendAsync(context, @"{""result"":403,""version"":""2020-04-20""}");
        }

        static async Task SendAsync(HttpContext context, string body)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body);

            var mediaType = new MediaTypeHeaderValue("application/json");
            mediaType.Encoding = Encoding.UTF8;
            context.Response.ContentType = mediaType.ToString();
            context.Response.ContentLength = buffer.Length;

            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length);
        }

        static async Task<Option<string>> ReadRequestBodyAsStringAsync(HttpRequest request)
        {
            var requestBodyBuffer = GetRequestBodyBuffer(request);

            var result = default(string);
            var bytesRead = default(int);

            try
            {
                var startTime = DateTime.Now;
                do
                {
                    bytesRead += await request.Body.ReadAsync(requestBodyBuffer, 0 + bytesRead, requestBodyBuffer.Length - bytesRead, GetTimeoutToken());
                }
                while (NoRequestTimeout(startTime) && IsKnownPayloadSize(request) && bytesRead < requestBodyBuffer.Length);

                if (IsKnownPayloadSize(request) && bytesRead < requestBodyBuffer.Length)
                {
                    Events.CouldNotReadFull();
                    return Option.None<string>();
                }
            }
            catch (Exception e)
            {
                Events.ErrorReadingStream(e);
                return Option.None<string>();
            }

            try
            {
                result = Encoding.UTF8.GetString(requestBodyBuffer, 0, bytesRead);
            }
            catch (Exception e)
            {
                Events.ErrorDecodingStream(e);
                return Option.None<string>();
            }

            return Option.Some<string>(result);
        }

        static byte[] GetRequestBodyBuffer(HttpRequest request)
        {
            // the request may contain the content-length in the header - or not
            var requestSize = IsKnownPayloadSize(request) ? request.ContentLength.Value : MaxInBufferSize;

            return new byte[requestSize];
        }

        static bool IsKnownPayloadSize(HttpRequest request)
        {
            // if the client sends too big number in the header - don't believe it
            // in that case use a maximum and treat the size as unknown
            return request.ContentLength.HasValue && request.ContentLength.Value < MaxInBufferSize;
        }

        static bool NoRequestTimeout(DateTime startTime)
        {
            return DateTime.Now.Subtract(startTime) > requestTimeout;
        }

        static CancellationToken GetTimeoutToken()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(requestTimeout);

            return cts.Token;
        }

        static bool IsPost(string verb) => string.Equals(verb, Post, StringComparison.OrdinalIgnoreCase);

        static Option<AuthRequest> Decode(Option<string> request)
        {
            try
            {
                return request.Map(s => JsonConvert.DeserializeObject<AuthRequest>(s));
            }
            catch (Exception e)
            {
                Events.ErrorDecodingPayload(e);
                return Option.None<AuthRequest>();
            }
        }

        static X509Certificate2 DecodeCertificate(string encodedCertificate)
        {
            try
            {
                var certificateContent = Convert.FromBase64String(encodedCertificate);
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

        static void SetupTimeouts(HttpListener listener)
        {
            listener.TimeoutManager.EntityBody = requestTimeout;
            listener.TimeoutManager.HeaderWait = requestTimeout;
        }

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentProtocolHead + 100;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentRequestHandler>();

            enum EventIds
            {
                AuthRequestReceived,
                AuthSucceeded,
                AuthFailed,
                CouldNotReadFull,
                ErrorReadingStream,
                ErrorDecodingStream,
                ErrorDecodingPayload,
                ErrorDecodingCertificate,
                ErrorHandlingRequest,
                NoCredentialsSpecified,
                MoreCredentialsSpecified,
                InvalidUsernameFormat,
                InvalidCredentials,
                ErrorSendingResponse
            }

            public static void AuthRequestReceived() => Log.LogInformation((int)EventIds.AuthRequestReceived, "AUTH request received");
            public static void AuthSucceeded(string id) => Log.LogInformation((int)EventIds.AuthSucceeded, "AUTH succeeded {0}", id);
            public static void AuthFailed(string id) => Log.LogWarning((int)EventIds.AuthSucceeded, "AUTH failed {0}", id);
            public static void CouldNotReadFull() => Log.LogError((int)EventIds.CouldNotReadFull, "Could not read AUTH request, parts missing");
            public static void ErrorReadingStream(Exception e) => Log.LogError((int)EventIds.ErrorReadingStream, e, "Error reading AUTH request");
            public static void ErrorDecodingStream(Exception e) => Log.LogWarning((int)EventIds.ErrorDecodingStream, e, "Error decoding AUTH request, UTF8 encoding expected");
            public static void ErrorDecodingPayload(Exception e) => Log.LogWarning((int)EventIds.ErrorDecodingPayload, e, "Error decoding AUTH request, invalid JSON structure");
            public static void ErrorDecodingCertificate(Exception e) => Log.LogWarning((int)EventIds.ErrorDecodingCertificate, e, "Error decoding certificate");
            public static void ErrorHandlingRequest(Exception e) => Log.LogError((int)EventIds.ErrorHandlingRequest, e, "Error handling request");
            public static void NoCredentialsSpecified() => Log.LogWarning((int)EventIds.NoCredentialsSpecified, "No credentials specified: either a certificate or a SAS token must be present for AUTH");
            public static void MoreCredentialsSpecified() => Log.LogWarning((int)EventIds.MoreCredentialsSpecified, "More credentials specified: only a certificate or a SAS token must be present for AUTH");
            public static void InvalidUsernameFormat(Exception e) => Log.LogWarning((int)EventIds.InvalidUsernameFormat, e, "Invalid username format provided for AUTH");
            public static void InvalidCredentials(Exception e) => Log.LogWarning((int)EventIds.InvalidCredentials, e, "Invalid credentials provided for AUTH");
            public static void ErrorSendingResponse(Exception e) => Log.LogError((int)EventIds.ErrorSendingResponse, e, "Error sending AUTH response");
        }
    }
}
