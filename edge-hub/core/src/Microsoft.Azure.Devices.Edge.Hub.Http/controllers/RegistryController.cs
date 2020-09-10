// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>()))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                string targetId = $"{deviceId}/{moduleId}";
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, edgeDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.CreateOrUpdateModuleAsync), edgeDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                var requestData = new CreateOrUpdateModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", module);
                RegistryApiHttpResult result = await this.apiClient.PutModuleAsync(edgeDeviceId, requestData, ifMatchHeader);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        [HttpPost]
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

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                string targetId = $"{requestData.Module.DeviceId}/{requestData.Module.Id}";
                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.PutModuleAsync(
                    edgeDeviceId,
                    new CreateOrUpdateModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", requestData.Module),
                    ifMatchHeader);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), ex);
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>()))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                string targetId = $"{deviceId}/{moduleId}";
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, edgeDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.GetModuleAsync), edgeDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                var requestData = new GetModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", moduleId);
                RegistryApiHttpResult result = await this.apiClient.GetModuleAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.GetModuleAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleAsync), ex);
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

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                string targetId = $"{targetDeviceId}/{requestData.ModuleId}";
                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.GetModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.GetModuleAsync(
                    edgeDeviceId,
                    new GetModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", requestData.ModuleId));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.GetModuleOnBehalfOfAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleOnBehalfOfAsync), ex);
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>()))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, edgeDeviceId, Constants.EdgeHubModuleId, deviceId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.ListModulesAsync), edgeDeviceId, Constants.EdgeHubModuleId, deviceId, authChain);
                var requestData = new ListModulesOnBehalfOfData($"{authChain}");
                RegistryApiHttpResult result = await this.apiClient.ListModulesAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.ListModulesAsync), edgeDeviceId, deviceId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesAsync), ex);
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

                if (!AuthChainHelpers.TryGetTargetDeviceId(requestData.AuthChain, out string targetId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.ListModulesOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.ListModulesOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.ListModulesAsync(
                    edgeDeviceId,
                    new ListModulesOnBehalfOfData($"{authChain}"));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.ListModulesOnBehalfOfAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesOnBehalfOfAsync), ex);
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

                if (!await this.AuthenticateAsync(deviceId, Option.None<string>()))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                IEdgeHub edgeHub = await this.edgeHubGetter;
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                string targetId = $"{deviceId}/{moduleId}";
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, edgeDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.DeleteModuleAsync), edgeDeviceId, Constants.EdgeHubModuleId, deviceId, authChain);
                var requestData = new DeleteModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", moduleId);
                RegistryApiHttpResult result = await this.apiClient.DeleteModuleAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.DeleteModuleAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleAsync), ex);
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

                if (!await this.AuthenticateAsync(actorDeviceId, Option.Some<string>(Constants.EdgeHubModuleId)))
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                string targetId = $"{targetDeviceId}/{requestData.ModuleId}";
                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.DeleteModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                RegistryApiHttpResult result = await this.apiClient.DeleteModuleAsync(
                    edgeDeviceId,
                    new DeleteModuleOnBehalfOfData($"{AuthChainHelpers.SkipFirstIdentityFromAuthChain(authChain)}", requestData.ModuleId));
                await this.SendResponseAsync(result.StatusCode, result.JsonContent);
                Events.CompleteRequest(nameof(this.DeleteModuleOnBehalfOfAsync), edgeDeviceId, targetId, requestData.AuthChain, result);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, FormatErrorResponseMessage(ex.ToString()));
            }
        }

        async Task<bool> AuthenticateAsync(string deviceId, Option<string> moduleId)
        {
            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, moduleId, this.HttpContext);

            if (authResult.Authenticated)
            {
                Events.Authenticated(deviceId, moduleId.GetOrElse(string.Empty));
                return true;
            }

            Events.AuthenticateFail(deviceId, moduleId.GetOrElse(string.Empty));
            return false;
        }

        async Task<(bool, string)> AuthorizeActorAsync(IDeviceScopeIdentitiesCache identitiesCache, string actorDeviceId, string actorModuleId, string targetId)
        {
            if (actorModuleId != Constants.EdgeHubModuleId)
            {
                // Only child EdgeHubs are allowed to act OnBehalfOf of devices/modules.
                Events.AuthorizationFail_BadActor(actorDeviceId, actorModuleId, targetId);
                return (false, string.Empty);
            }

            // Actor device is claiming to be our child, and that the target device is its child.
            // So we should have an authchain already cached for the target device.
            Option<string> targetAuthChainOption = await identitiesCache.GetAuthChain(targetId);

            if (!targetAuthChainOption.HasValue)
            {
                Events.AuthorizationFail_NoAuthChain(targetId);
                return (false, string.Empty);
            }

            // Validate the target auth-chain
            string targetAuthChain = targetAuthChainOption.Expect(() => new InvalidOperationException());
            if (!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetId, targetAuthChain))
            {
                Events.AuthorizationFail_InvalidAuthChain(actorDeviceId, targetId, targetAuthChain);
                return (false, string.Empty);
            }

            return (true, targetAuthChain);
        }

        async Task SendResponseAsync(HttpStatusCode status, string jsonContent = "")
        {
            this.Response.StatusCode = (int)status;

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
                    $"Received request in {source}: deviceId/moduleId={deviceId}/{moduleId}");
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
                    $"Authenticated: deviceId/moduleId={deviceId}/{moduleId}");
            }

            public static void AuthenticateFail(string deviceId, string moduleId = "")
            {
                Log.LogError((int)EventIds.AuthenticateFail, $"AuthentifcateFail: deviceId/moduleId={deviceId}/{moduleId}");
            }

            public static void Authorized(string source, string deviceId, string moduleId, string targetDeviceId, string authChain)
            {
                Log.LogInformation(
                    (int)EventIds.Authorized,
                    $"Authorized in {source}: deviceId/moduleId={deviceId}/{moduleId}, targetDeviceId={targetDeviceId}, authChain={authChain}");
            }

            public static void AuthorizationFail_BadActor(string actorDeviceId, string actorModuleId, string targetId)
            {
                Log.LogError((int)EventIds.AuthorizationFail_BadActor, $"{actorDeviceId}/{actorModuleId} not authorized to connect OnBehalfOf {targetId}");
            }

            public static void AuthorizationFail_NoAuthChain(string targetId)
            {
                Log.LogError((int)EventIds.AuthorizationFail_NoAuthChain, $"No auth chain for target identity: {targetId}");
            }

            public static void AuthorizationFail_InvalidAuthChain(string actorId, string targetId, string authChain)
            {
                Log.LogError((int)EventIds.AuthorizationFail_InvalidAuthChain, $"Invalid auth chain, actor: {actorId}, target: {targetId}, auth chain: {authChain}");
            }

            public static void CompleteRequest(string source, string deviceId, string targetId, string authChain, RegistryApiHttpResult result)
            {
                Log.LogInformation(
                    (int)EventIds.Authenticated,
                    $"CompleteRequest in {source}: deviceId={deviceId}, targetId={targetId}, authChain={authChain} {Environment.NewLine} {result.StatusCode}:{result.JsonContent}");
            }
        }
    }
}
