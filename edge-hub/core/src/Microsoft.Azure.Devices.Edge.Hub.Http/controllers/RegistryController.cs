// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
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
            [FromHeader(Name="if-match")] string ifMatchHeader,
            [FromBody] Module module)
        {
            try
            {
                Events.ReceivedRequest(nameof(this.CreateOrUpdateModuleAsync), deviceId, moduleId);

                try
                {
                    deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
                    moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
                    Preconditions.CheckNotNull(module, nameof(module));

                    if (!string.Equals(deviceId, module.DeviceId) || !string.Equals(moduleId, module.Id))
                    {
                        throw new ApplicationException("Device Id or module Id doesn't match between request URI and body.");
                    }
                }
                catch (Exception ex)
                {
                    Events.BadRequest(nameof(this.CreateOrUpdateModuleAsync), ex.Message);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(ex.Message));
                    return;
                }

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>()))
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>()))
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>()))
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>(), Option.None<string>()))
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
                if (!AuthChainHelpers.TryGetTargetDeviceId(requestData.AuthChain, out string targetDeviceId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!string.Equals(targetDeviceId, requestData.Module.DeviceId, StringComparison.Ordinal))
                {
                    string errorMessage = $"Target device Id does not match between auth chain ({targetDeviceId}) and request body ({requestData.Module.DeviceId}).";
                    Events.BadRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), errorMessage);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage(errorMessage));
                    return;
                }

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(requestData.AuthChain)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(targetDeviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(requestData.Module.DeviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.PutModuleAsync(
                    edgeDeviceId,
                    new CreateOrUpdateModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", requestData.Module),
                    ifMatchHeader);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain.OrDefault(), result);
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
                if (!AuthChainHelpers.TryGetTargetDeviceId(requestData.AuthChain, out string targetDeviceId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.GetModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(requestData.AuthChain)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(targetDeviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(targetDeviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.GetModuleAsync(
                    edgeDeviceId,
                    new GetModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", requestData.ModuleId));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.GetModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain.OrDefault(), result);
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

                if (!AuthChainHelpers.TryGetTargetDeviceId(requestData.AuthChain, out string targetDeviceId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.ListModulesOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(requestData.AuthChain)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(targetDeviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(targetDeviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string targetAuthChainVal = targetAuthChain.OrDefault();
                if(!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetDeviceId, targetAuthChainVal))
                {
                    Events.AuthorizationFail_NoAuthChain(targetDeviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.ListModulesAsync(
                    edgeDeviceId,
                    new ListModulesOnBehalfOfData($"{targetAuthChain.OrDefault()}"));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.ListModulesOnBehalfOfAsync), edgeDeviceId, targetAuthChain.OrDefault(), result);
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

                if (!AuthChainHelpers.TryGetTargetDeviceId(requestData.AuthChain, out string targetDeviceId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.DeleteModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(requestData.AuthChain)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                Option<string> targetAuthChain = await identitiesCache.GetAuthChain(targetDeviceId);
                if (!targetAuthChain.HasValue)
                {
                    Events.AuthorizationFail_NoAuthChain(targetDeviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.DeleteModuleAsync(
                    edgeDeviceId,
                    new DeleteModuleOnBehalfOfData($"{targetAuthChain.OrDefault()}", requestData.ModuleId));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.DeleteModuleOnBehalfOfAsync), edgeDeviceId, targetAuthChain.OrDefault(), result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        async Task<bool> AuthenticateAsync(string deviceId, Option<string> moduleId, Option<string> authChain)
        {
            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, moduleId, authChain, this.HttpContext);

            if (authResult.Authenticated)
            {
                Events.Authenticated(deviceId, moduleId.GetOrElse(string.Empty));
                return true;
            }

            Events.AuthenticateFail(deviceId, moduleId.GetOrElse(string.Empty));
            return false;
        }

        async Task SendResponseAsync(HttpStatusCode status, string jsonContent = "")
        {
            await this.SendResponseAsync(status, new Dictionary<string, string>(), jsonContent);
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
        }
    }
}
