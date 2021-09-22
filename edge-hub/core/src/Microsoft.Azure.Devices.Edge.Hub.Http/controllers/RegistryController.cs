// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class RegistryController : Controller
    {
        const string ContentType = "application/json; charset=utf-8";

        readonly IRegistryOnBehalfOfApiClient apiClient;
        readonly Task<IHttpRequestAuthenticator> authenticatorGetter;
        readonly Task<IEdgeHub> edgeHubGetter;
        static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeController>();

        public RegistryController(
            IRegistryOnBehalfOfApiClient apiClient,
            Task<IEdgeHub> edgeHubGetter,
            Task<IHttpRequestAuthenticator> authenticatorGetter)
        {
            this.apiClient = Preconditions.CheckNotNull(apiClient, nameof(apiClient));
            this.authenticatorGetter = Preconditions.CheckNotNull(authenticatorGetter, nameof(authenticatorGetter));
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHubGetter, nameof(edgeHubGetter));
        }

        [HttpPut]
        [Route("devices/{deviceId}/modules/{moduleId}")]
        public async Task CreateOrUpdateModuleAsync(
            [FromRoute] string deviceId,
            [FromRoute] string moduleId,
            [FromHeader(Name = "if-match")] string ifMatchHeader,
            [FromBody] Module module)
        {
            try
            {
                Log.LogError("enter");
                Events.ReceivedRequest(nameof(this.CreateOrUpdateModuleAsync), deviceId, moduleId);

                try
                {
                    deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
                    moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
                    Preconditions.CheckNotNull(module, nameof(module));

                    Log.LogError("check1");
                    if (!string.Equals(deviceId, module.DeviceId) || !string.Equals(moduleId, module.Id))
                    {
                        throw new ApplicationException("Device Id or module Id doesn't match between request URI and body.");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError("check2");
                    Events.BadRequest(nameof(this.CreateOrUpdateModuleAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.Message));
                    return;
                }

                Log.LogError("check3");
                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                if (!await AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>(), this.HttpContext, authenticator))
                {
                    Log.LogError("check4");
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Log.LogError("check5");
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(deviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                var requestData = new CreateOrUpdateModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", module);
                RegistryApiHttpResult result = await this.apiClient.PutModuleAsync(edgeDeviceId, requestData, ifMatchHeader);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleAsync), edgeDeviceId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpGet]
        [Route("devices/{deviceId}/modules/{moduleId}")]
        public async Task GetModuleAsync(
            [FromRoute] string deviceId,
            [FromRoute] string moduleId)
        {
            try
            {
                Events.ReceivedRequest(nameof(this.GetModuleAsync), deviceId, moduleId);

                try
                {
                    deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
                    moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.GetModuleAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                if (!await AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>(), this.HttpContext, authenticator))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(deviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                var requestData = new GetModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", moduleId);
                RegistryApiHttpResult result = await this.apiClient.GetModuleAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.GetModuleAsync), edgeDeviceId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpGet]
        [Route("devices/{deviceId}/modules")]
        public async Task ListModulesAsync(
            [FromRoute] string deviceId)
        {
            try
            {
                Events.ReceivedRequest(nameof(this.ListModulesAsync), deviceId);

                if (!this.HttpContext.Request.Query.ContainsKey("api-version"))
                {
                    Dictionary<string, string> headers = new Dictionary<string, string>();
                    headers.Add("iothub-errorcode", "InvalidProtocolVersion");
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, headers, string.Empty);
                    return;
                }

                try
                {
                    deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.ListModulesAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                if (!await AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>(), this.HttpContext, authenticator))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(deviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                var requestData = new ListModulesOnBehalfOfData($"{targetAuthChain.OrDefault()}");
                RegistryApiHttpResult result = await this.apiClient.ListModulesAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.ListModulesAsync), edgeDeviceId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpDelete]
        [Route("devices/{deviceId}/modules/{moduleId}")]
        public async Task DeleteModuleAsync(
            [FromRoute] string deviceId,
            [FromRoute] string moduleId)
        {
            try
            {
                Events.ReceivedRequest(nameof(this.DeleteModuleAsync), deviceId, moduleId);

                try
                {
                    deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
                    moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.DeleteModuleAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                if (!await AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>(), this.HttpContext, authenticator))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(deviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                var requestData = new DeleteModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", moduleId);
                RegistryApiHttpResult result = await this.apiClient.DeleteModuleAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.DeleteModuleAsync), edgeDeviceId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpPut]
        [Route("devices/{actorDeviceId}/modules/$edgeHub/putModuleOnBehalfOf")]
        public async Task CreateOrUpdateModuleOnBehalfOfAsync(
            [FromRoute] string actorDeviceId,
            [FromHeader(Name = "if-match")] string ifMatchHeader,
            [FromBody] CreateOrUpdateModuleOnBehalfOfData requestData)
        {
            try
            {
                Events.ReceivedOnBehalfOfRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), actorDeviceId, Events.GetAdditionalInfo(requestData));

                try
                {
                    Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
                    Preconditions.CheckNotNull(requestData, nameof(requestData));
                    Preconditions.CheckNonWhiteSpace(requestData.AuthChain, nameof(requestData.AuthChain));
                    Preconditions.CheckNotNull(requestData.Module, nameof(requestData.Module));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.ToString()));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                Try<string> targetAuthChainTry = await AuthorizeOnBehalfOf(actorDeviceId, requestData.AuthChain, nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), this.HttpContext, edgeHub, authenticator);
                if (targetAuthChainTry.Success)
                {
                    string targetAuthChain = targetAuthChainTry.Value;
                    string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                    RegistryApiHttpResult result = await this.apiClient.PutModuleAsync(
                        edgeDeviceId,
                        new CreateOrUpdateModuleOnBehalfOfData($"{targetAuthChain}", requestData.Module),
                        ifMatchHeader);
                    await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                    Events.CompleteRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain, result);
                }
                else
                {
                    await this.SendErrorResponse(targetAuthChainTry.Exception);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/$edgeHub/getModuleOnBehalfOf")]
        public async Task GetModuleOnBehalfOfAsync(
            [FromRoute] string actorDeviceId,
            [FromBody] GetModuleOnBehalfOfData requestData)
        {
            try
            {
                Events.ReceivedOnBehalfOfRequest(nameof(this.GetModuleOnBehalfOfAsync), actorDeviceId, Events.GetAdditionalInfo(requestData));

                try
                {
                    Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
                    Preconditions.CheckNotNull(requestData, nameof(requestData));
                    Preconditions.CheckNonWhiteSpace(requestData.AuthChain, nameof(requestData.AuthChain));
                    Preconditions.CheckNonWhiteSpace(requestData.ModuleId, nameof(requestData.ModuleId));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.GetModuleOnBehalfOfAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.ToString()));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                Try<string> targetAuthChainTry = await AuthorizeOnBehalfOf(actorDeviceId, requestData.AuthChain, nameof(this.GetModuleOnBehalfOfAsync), this.HttpContext, edgeHub, authenticator);
                if (targetAuthChainTry.Success)
                {
                    string targetAuthChain = targetAuthChainTry.Value;
                    string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                    RegistryApiHttpResult result = await this.apiClient.GetModuleAsync(
                        edgeDeviceId,
                        new GetModuleOnBehalfOfData($"{targetAuthChain}", requestData.ModuleId));
                    await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                    Events.CompleteRequest(nameof(this.GetModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain, result);
                }
                else
                {
                    await this.SendErrorResponse(targetAuthChainTry.Exception);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/$edgeHub/getModulesOnTargetDevice")]
        public async Task ListModulesOnBehalfOfAsync(
            [FromRoute] string actorDeviceId,
            [FromBody] ListModulesOnBehalfOfData requestData)
        {
            try
            {
                Events.ReceivedOnBehalfOfRequest(nameof(this.ListModulesOnBehalfOfAsync), actorDeviceId, Events.GetAdditionalInfo(requestData));

                try
                {
                    Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
                    Preconditions.CheckNotNull(requestData, nameof(requestData));
                    Preconditions.CheckNonWhiteSpace(requestData.AuthChain, nameof(requestData.AuthChain));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.ListModulesOnBehalfOfAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.ToString()));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                Try<string> targetAuthChainTry = await AuthorizeOnBehalfOf(actorDeviceId, requestData.AuthChain, nameof(this.ListModulesOnBehalfOfAsync), this.HttpContext, edgeHub, authenticator);
                if (targetAuthChainTry.Success)
                {
                    string targetAuthChain = targetAuthChainTry.Value;
                    string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                    RegistryApiHttpResult result = await this.apiClient.ListModulesAsync(
                        edgeDeviceId,
                        new ListModulesOnBehalfOfData($"{targetAuthChain}"));
                    await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                    Events.CompleteRequest(nameof(this.ListModulesOnBehalfOfAsync), edgeDeviceId, targetAuthChain, result);
                }
                else
                {
                    await this.SendErrorResponse(targetAuthChainTry.Exception);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/$edgeHub/deleteModuleOnBehalfOf")]
        public async Task DeleteModuleOnBehalfOfAsync(
            [FromRoute] string actorDeviceId,
            [FromBody] DeleteModuleOnBehalfOfData requestData)
        {
            try
            {
                Events.ReceivedOnBehalfOfRequest(nameof(this.DeleteModuleOnBehalfOfAsync), actorDeviceId, Events.GetAdditionalInfo(requestData));

                try
                {
                    Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
                    Preconditions.CheckNotNull(requestData, nameof(requestData));
                    Preconditions.CheckNonWhiteSpace(requestData.AuthChain, nameof(requestData.AuthChain));
                    Preconditions.CheckNonWhiteSpace(requestData.ModuleId, nameof(requestData.ModuleId));
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.DeleteModuleOnBehalfOfAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.ToString()));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;

                Try<string> targetAuthChainTry = await AuthorizeOnBehalfOf(actorDeviceId, requestData.AuthChain, nameof(this.DeleteModuleOnBehalfOfAsync), this.HttpContext, edgeHub, authenticator);
                if (targetAuthChainTry.Success)
                {
                    string targetAuthChain = targetAuthChainTry.Value;
                    string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                    RegistryApiHttpResult result = await this.apiClient.DeleteModuleAsync(
                        edgeDeviceId,
                        new DeleteModuleOnBehalfOfData($"{targetAuthChain}", requestData.ModuleId));
                    await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                    Events.CompleteRequest(nameof(this.DeleteModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain, result);
                }
                else
                {
                    await this.SendErrorResponse(targetAuthChainTry.Exception);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        internal static async Task<Try<string>> AuthorizeOnBehalfOf(
            string actorDeviceId,
            string authChain,
            string source,
            HttpContext httpContext,
            IEdgeHub edgeHub,
            IHttpRequestAuthenticator authenticator)
        {
            if (!AuthChainHelpers.TryGetTargetDeviceId(authChain, out string targetDeviceId))
            {
                Events.InvalidRequestAuthChain(source, authChain);
                return Try<string>.Failure(new ValidationException(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {authChain}.")));
            }

            if (!await AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(authChain), httpContext, authenticator))
            {
                return Try<string>.Failure(new ValidationException(HttpStatusCode.Unauthorized));
            }

            IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
            Option<string> targetAuthChain = await identitiesCache.GetAuthChain(targetDeviceId);
            return targetAuthChain.Match(
                ac =>
                {
                    if (!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetDeviceId, ac))
                    {
                        Events.AuthorizationFail_InvalidAuthChain(actorDeviceId, targetDeviceId, ac);
                        return Try<string>.Failure(new ValidationException(HttpStatusCode.Unauthorized));
                    }

                    return ac;
                },
                () =>
                {
                    Events.AuthorizationFail_NoAuthChain(targetDeviceId);
                    return Try<string>.Failure(new ValidationException(HttpStatusCode.Unauthorized));
                });
        }

        static async Task<bool> AuthenticateAsync(
            string deviceId,
            Option<string> moduleId,
            Option<string> authChain,
            HttpContext httpContext,
            IHttpRequestAuthenticator authenticator)
        {
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, moduleId, authChain, httpContext);

            if (authResult.Authenticated)
            {
                Events.Authenticated(deviceId, moduleId.GetOrElse(string.Empty));
                return true;
            }

            Events.AuthenticateFail(deviceId, moduleId.GetOrElse(string.Empty));
            return false;
        }

        Task SendErrorResponse(Exception ex)
        {
            if (ex is ValidationException ve)
            {
                return this.SendResponseAsync(ve.StatusCode, FormatErrorResponseMessage(ve.Message));
            }
            else
            {
                return this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.Message));
            }
        }

        Task SendResponseAsync(HttpStatusCode status, string jsonContent = "")
        {
            return this.SendResponseAsync(status, new Dictionary<string, string>(), jsonContent);
        }

        async Task SendResponseAsync(HttpStatusCode status, Dictionary<string, string> headers, string jsonContent = "")
        {
            this.Response.StatusCode = (int)status;

            foreach (var header in headers)
            {
                this.Response.Headers.Add(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(jsonContent))
            {
                var resultUtf8Bytes = Encoding.UTF8.GetBytes(jsonContent);

                this.Response.ContentLength = resultUtf8Bytes.Length;
                this.Response.ContentType = ContentType;

                await this.Response.Body.WriteAsync(resultUtf8Bytes, 0, resultUtf8Bytes.Length);
            }
        }

        static string FormatErrorResponseMessage(string errorMessage)
        {
            return $"{{ \"errorMessage\": \"{HttpUtility.JavaScriptStringEncode(errorMessage)}\" }}";
        }

        internal class ValidationException : Exception
        {
            public ValidationException(HttpStatusCode statusCode, string jsonContent = "")
                : base(jsonContent ?? string.Empty)
            {
                this.StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
        }

        static class Events
        {
            const int IdStart = HttpEventIds.RegistryController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RegistryController>();

            enum EventIds
            {
                BadRequest = IdStart,
                InvalidRequestAuthChain,
                InternalServerError,
                ReceivedRequest,
                ReceivedOnBehalfOfRequest,
                Authenticated,
                Authorized,
                AuthenticateFail,
                AuthorizationFail_BadActor,
                AuthorizationFail_NoAuthChain,
                AuthorizationFail_InvalidAuthChain,
                CompleteRequest
            }

            public static string GetAdditionalInfo(CreateOrUpdateModuleOnBehalfOfData data)
            {
                return $"authChain={data.AuthChain}, targetId={data.Module.Id}/{data.Module.DeviceId}";
            }

            public static string GetAdditionalInfo(GetModuleOnBehalfOfData data)
            {
                return $"authChain={data.AuthChain}, targetModuleId={data.ModuleId}";
            }

            public static string GetAdditionalInfo(ListModulesOnBehalfOfData data)
            {
                return $"authChain={data.AuthChain}";
            }

            public static string GetAdditionalInfo(DeleteModuleOnBehalfOfData data)
            {
                return $"authChain={data.AuthChain}, targetModuleId={data.ModuleId}";
            }

            public static void BadRequest(string source, string message)
            {
                Log.LogError((int)EventIds.BadRequest, $"Bad request in {source}: {message}");
            }

            public static void InvalidRequestAuthChain(string source, string authChain)
            {
                Log.LogError((int)EventIds.InvalidRequestAuthChain, $"Invalid auth chain in {source}: {authChain}");
            }

            public static void InternalServerError(string source, Exception ex)
            {
                Log.LogError((int)EventIds.InternalServerError, $"Unexpected exception in {source}: {ex}");
            }

            public static void ReceivedRequest(string source, string deviceId, string moduleId = "")
            {
                Log.LogInformation(
                    (int)EventIds.ReceivedRequest,
                    $"Received request in {source}: deviceId={deviceId}, moduleId={moduleId}");
            }

            public static void ReceivedOnBehalfOfRequest(string source, string actorDeviceId, string additionalInfo)
            {
                Log.LogInformation(
                    (int)EventIds.ReceivedOnBehalfOfRequest,
                    $"Received onbehalfof request in {source}: actorDeviceId={actorDeviceId}, {additionalInfo}");
            }

            public static void Authenticated(string deviceId, string moduleId = "")
            {
                Log.LogInformation(
                    (int)EventIds.Authenticated,
                    $"Authenticated: deviceId={deviceId}, moduleId={moduleId}");
            }

            public static void AuthenticateFail(string deviceId, string moduleId = "")
            {
                Log.LogError((int)EventIds.AuthenticateFail, $"AuthentifcateFail: deviceId={deviceId}, moduleId={moduleId}");
            }

            public static void AuthorizationFail_NoAuthChain(string targetId)
            {
                Log.LogError((int)EventIds.AuthorizationFail_NoAuthChain, $"No auth chain for target identity: {targetId}");
            }

            public static void CompleteRequest(string source, string deviceId, string authChain, RegistryApiHttpResult result)
            {
                Log.LogInformation(
                    (int)EventIds.Authenticated,
                    $"CompleteRequest in {source}: deviceId={deviceId}, authChain={authChain}, status={result.StatusCode}");
            }

            internal static void AuthorizationFail_InvalidAuthChain(string actorDeviceId, string targetDeviceId, string authChain)
            {
                Log.LogError((int)EventIds.AuthorizationFail_InvalidAuthChain, $"Target device {targetDeviceId} is not a child of {actorDeviceId}, auth chain found is {authChain}");
            }
        }
    }
}
