// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;
    using static System.FormattableString;

    public class AuthenticationMiddleware
    {
        readonly RequestDelegate next;
        readonly IAuthenticator authenticator;
        readonly IIdentityFactory identityFactory;
        readonly string iotHubName;

        public AuthenticationMiddleware(
            RequestDelegate next,
            IAuthenticator authenticator,
            IIdentityFactory identityFactory,
            string iotHubName)
        {
            this.next = next;
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.identityFactory = Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                (bool isAuthenticated, string errorMessage) result = await this.AuthenticateRequest(context);
                if (result.isAuthenticated)
                {
                    await this.next.Invoke(context);
                }
                else
                {
                    await WriteErrorResponse(context, result.errorMessage);
                }
            }
            catch (Exception ex)
            {
                Events.AuthenticationError(ex, context);
                await WriteErrorResponse(context, "Unknown error occurred during authentication.");
            }
        }

        internal async Task<(bool, string)> AuthenticateRequest(HttpContext context)
        {
            // Authorization header may be present in the QueryNameValuePairs as per Azure standards,           
            // So check in the query parameters first.             
            List<string> authorizationQueryParameters = context.Request.Query
                .Where(p => p.Key.Equals(HeaderNames.Authorization, StringComparison.OrdinalIgnoreCase))
                .SelectMany(p => p.Value)
                .ToList();

            if (!(context.Request.Headers.TryGetValue(HeaderNames.Authorization, out StringValues authorizationHeaderValues)
                && authorizationQueryParameters.Count == 0))
            {
                return LogAndReturnFailure("Authorization header missing");
            }
            else if (authorizationQueryParameters.Count != 1 && authorizationHeaderValues.Count != 1)
            {
                return LogAndReturnFailure("Invalid authorization header count");
            }

            string authHeader = authorizationQueryParameters.Count == 1
                ? authorizationQueryParameters.First()
                : authorizationHeaderValues.First();

            if (!authHeader.StartsWith("SharedAccessSignature", StringComparison.OrdinalIgnoreCase))
            {
                return LogAndReturnFailure("Invalid Authorization header. Only SharedAccessSignature is supported.");
            }

            SharedAccessSignature sharedAccessSignature;
            try
            {
                sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubName, authHeader);
                if (sharedAccessSignature.IsExpired())
                {
                    return LogAndReturnFailure("SharedAccessSignature is expired");
                }
            }
            catch (Exception ex)
            {
                return LogAndReturnFailure($"Cannot parse SharedAccessSignature because of the following error - {ex.Message}");
            }

            if (!context.Request.Headers.TryGetValue(HttpConstants.ModuleIdHeaderKey, out StringValues moduleIds) || moduleIds.Count == 0)
            {
                return LogAndReturnFailure("Request header does not contain ModuleId");
            }
            string moduleId = moduleIds.First();

            string userName = $"{this.iotHubName}/{moduleId}";
            Try<IIdentity> identityTry = this.identityFactory.GetWithSasToken(userName, authHeader);
            if (!identityTry.Success)
            {
                return LogAndReturnFailure("Unable to get identity for the device", identityTry.Exception);
            }

            IIdentity identity = identityTry.Value;
            if (!await this.authenticator.AuthenticateAsync(identity))
            {
                return LogAndReturnFailure($"Unable to authenticate device with Id {identity.Id}");
            }

            context.Items.Add(HttpConstants.IdentityKey, identity);
            Events.AuthenticationSucceeded(identity);
            return (true, string.Empty);
        }

        static (bool, string) LogAndReturnFailure(string message, Exception ex = null)
        {
            Events.AuthenticationFailed(message, ex);
            return (false, message);
        }

        static async Task WriteErrorResponse(HttpContext context, string message)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync(message);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthenticationMiddleware>();
            const int IdStart = HttpEventIds.AuthenticationMiddleware;

            enum EventIds
            {
                AuthenticationFailed = IdStart,
                AuthenticationError,
                AuthenticationSuccess
            }

            public static void AuthenticationFailed(string message, Exception ex)
            {
                if (ex == null)
                {
                    Log.LogDebug((int)EventIds.AuthenticationFailed, Invariant($"Http Authentication failed due to following issue - {message}"));
                }
                else
                {
                    Log.LogWarning((int)EventIds.AuthenticationFailed, ex, Invariant($"Http Authentication failed due to following issue - {message}"));
                }
            }

            public static void AuthenticationError(Exception ex, HttpContext context)
            {
                // TODO - Check if it is okay to put request headers in logs.
                Log.LogError((int)EventIds.AuthenticationError, ex, Invariant($"Unknown error occurred during authentication, for request with headers - {context.Request.Headers}"));
            }

            public static void AuthenticationSucceeded(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticationSuccess, Invariant($"Http Authentication succeeded for device with Id {identity.Id}"));
            }
        }
    }

    public static class AuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder, string iotHubName)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>(iotHubName);
        }
    }
}
