// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Net;
    using System.Net.Http;
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

        readonly IRegistryApiClient apiClient;
        readonly Task<IHttpRequestAuthenticator> authenticatorGetter;
        readonly Task<IEdgeHub> edgeHubGetter;

        public RegistryController(
            IRegistryApiClient apiClient,
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, Option.None<string>(), this.HttpContext);

                if (!authResult.Authenticated)
                {
                    Events.AuthenticateFail(nameof(this.CreateOrUpdateModuleAsync), deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authenticated(nameof(this.CreateOrUpdateModuleAsync), deviceId);
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
                var requestData = new CreateOrUpdateModuleOnBehalfOfData { AuthChain = $"{authChain}", Module = module };
                HttpResponseMessage responseMessage = await this.apiClient.PutModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(responseMessage);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleAsync), edgeDeviceId, targetId, authChain);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
            }
        }

        [HttpPut]
        [Route("devices/{actorDeviceId}/putModuleOnBehalfOf")]
        public async Task CreateOrUpdateModuleOnBehalfOfAsync(
            [FromRoute] string actorDeviceId,
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);

                if (!AuthChainHelpers.TryGetTargetId(requestData.AuthChain, out string targetId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                if (!string.Equals(targetId, requestData.Module.DeviceId + "/" + requestData.Module.Id))
                {
                    string errorMessage = $"Target device Id does not match between auth chain ({targetId}) and request body ({requestData.Module.DeviceId + "/" + requestData.Module.Id}).";
                    Events.BadRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), errorMessage);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(errorMessage));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), this.HttpContext);

                if (!authResult.Authenticated)
                {
                    Events.AuthenticateFail(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authenticated(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                requestData.AuthChain = $"{authChain}";
                HttpResponseMessage responseMessage = await this.apiClient.PutModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(responseMessage);
                Events.CompleteRequest(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), edgeDeviceId, targetId, authChain);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.CreateOrUpdateModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, Option.None<string>(), this.HttpContext);

                if (authResult.Authenticated)
                {
                    Events.Authenticated(nameof(this.GetModuleAsync), deviceId);

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
                    var requestData = new GetModuleOnBehalfOfData { AuthChain = $"{authChain}", ModuleId = moduleId };
                    HttpResponseMessage responseMessage = await this.apiClient.GetModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                    await this.SendResponseAsync(responseMessage);
                    Events.CompleteRequest(nameof(this.GetModuleAsync), edgeDeviceId, targetId, authChain);
                }
                else
                {
                    Events.AuthenticateFail(nameof(this.GetModuleAsync), deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
            }
        }

        [HttpGet]
        [Route("devices/{actorId}/modules/$edgeHub/getModuleOnBehalfOf")]
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);

                if (!AuthChainHelpers.TryGetTargetId(requestData.AuthChain, out string targetId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.GetModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), this.HttpContext);

                if (!authResult.Authenticated)
                {
                    Events.AuthenticateFail(nameof(this.GetModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authenticated(nameof(this.GetModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.GetModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                requestData.AuthChain = $"{authChain}";
                HttpResponseMessage responseMessage = await this.apiClient.GetModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(responseMessage);
                Events.CompleteRequest(nameof(this.GetModuleOnBehalfOfAsync), edgeDeviceId, targetId, authChain);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.GetModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, Option.None<string>(), this.HttpContext);

                if (authResult.Authenticated)
                {
                    Events.Authenticated(nameof(this.ListModulesAsync), deviceId);

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
                    var requestData = new ListModulesOnBehalfOfData { AuthChain = $"{authChain}" };
                    HttpResponseMessage responseMessage = await this.apiClient.ListModulesOnBehalfOfAsync(edgeDeviceId, requestData);
                    await this.SendResponseAsync(responseMessage);
                    Events.CompleteRequest(nameof(this.ListModulesAsync), edgeDeviceId, deviceId, authChain);
                }
                else
                {
                    Events.AuthenticateFail(nameof(this.ListModulesAsync), deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/getModulesOnTargetDevice")]
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);

                if (!AuthChainHelpers.TryGetTargetId(requestData.AuthChain, out string targetId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.ListModulesOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), this.HttpContext);

                if (!authResult.Authenticated)
                {
                    Events.AuthenticateFail(nameof(this.ListModulesOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authenticated(nameof(this.ListModulesOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
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
                requestData.AuthChain = $"{authChain}";
                HttpResponseMessage responseMessage = await this.apiClient.ListModulesOnBehalfOfAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(responseMessage);
                Events.CompleteRequest(nameof(this.ListModulesOnBehalfOfAsync), edgeDeviceId, targetId, authChain);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.ListModulesOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex.Message));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, Option.None<string>(), this.HttpContext);

                if (authResult.Authenticated)
                {
                    Events.Authenticated(nameof(this.DeleteModuleAsync), deviceId);

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
                    var requestData = new DeleteModuleOnBehalfOfData { AuthChain = $"{authChain}", ModuleId = moduleId };
                    HttpResponseMessage responseMessage = await this.apiClient.DeleteModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                    await this.SendResponseAsync(responseMessage);
                    Events.CompleteRequest(nameof(this.DeleteModuleAsync), edgeDeviceId, targetId, authChain);
                }
                else
                {
                    Events.AuthenticateFail(nameof(this.DeleteModuleAsync), deviceId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                }
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/deleteModuleOnBehalfOf")]
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
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage(ex));
                    return;
                }

                actorDeviceId = WebUtility.UrlDecode(actorDeviceId);

                if (!AuthChainHelpers.TryGetTargetId(requestData.AuthChain, out string targetId))
                {
                    Events.InvalidRequestAuthChain(nameof(this.DeleteModuleOnBehalfOfAsync), requestData.AuthChain);
                    await this.SendResponseAsync(HttpStatusCode.BadRequest, this.FormatErrorResponseMessage($"Invalid request auth chain {requestData.AuthChain}."));
                    return;
                }

                IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), this.HttpContext);

                if (!authResult.Authenticated)
                {
                    Events.AuthenticateFail(nameof(this.DeleteModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authenticated(nameof(this.DeleteModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId);
                // Check that the actor device is authorized to act OnBehalfOf the target
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();

                (bool authorized, string authChain) = await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, Constants.EdgeHubModuleId, targetId);
                if (!authorized)
                {
                    await this.SendResponseAsync(HttpStatusCode.Unauthorized);
                    return;
                }

                Events.Authorized(nameof(this.DeleteModuleOnBehalfOfAsync), actorDeviceId, Constants.EdgeHubModuleId, targetId, authChain);
                string edgeDeviceId = edgeHub.GetEdgeDeviceId();
                requestData.AuthChain = $"{authChain}";
                HttpResponseMessage responseMessage = await this.apiClient.DeleteModuleOnBehalfOfAsync(edgeDeviceId, requestData);
                await this.SendResponseAsync(responseMessage);
                Events.CompleteRequest(nameof(this.DeleteModuleOnBehalfOfAsync), edgeDeviceId, targetId, authChain);
            }
            catch (Exception ex)
            {
                Events.InternalServerError(nameof(this.DeleteModuleOnBehalfOfAsync), ex);
                await this.SendResponseAsync(HttpStatusCode.InternalServerError, this.FormatErrorResponseMessage(ex));
            }
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

        async Task SendResponseAsync(HttpResponseMessage responseMessage)
        {
            string content = string.Empty;
            if (responseMessage.Content != null)
            {
                content = await responseMessage.Content.ReadAsStringAsync();
            }

            await this.SendResponseAsync(responseMessage.StatusCode, content);
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

        string FormatErrorResponseMessage(Exception ex)
        {
            return this.FormatErrorResponseMessage(ex.ToString());
        }

        string FormatErrorResponseMessage(string errorMessage)
        {
            return this.FormatJsonMessage("errorMessage", errorMessage);
        }

        string FormatJsonMessage(string key, string value)
        {
            return $"{{ \"{key}\": \"{HttpUtility.JavaScriptStringEncode(value)}\" }}";
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

            public static void Authenticated(string source, string deviceId, string moduleId = "")
            {
                Log.LogInformation(
                    (int)EventIds.Authenticated,
                    $"Authenticated in {source}: deviceId/moduleId={deviceId}/{moduleId}");
            }

            public static void AuthenticateFail(string source, string deviceId, string moduleId = "")
            {
                Log.LogError((int)EventIds.AuthenticateFail, $"AuthentifcateFail in {source}: deviceId/moduleId={deviceId}/{moduleId}");
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

            public static void CompleteRequest(string source, string deviceId, string targetId, string authChain)
            {
                Log.LogInformation(
                    (int)EventIds.Authenticated,
                    $"CompleteRequest in {source}: deviceId={deviceId}, targetId={targetId}, authChain={authChain}");
            }
        }
    }
}
