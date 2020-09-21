// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;

    public class DeviceScopeController : Controller
    {
        static readonly string SupportedContentType = "application/json; charset=utf-8";

        readonly Task<IEdgeHub> edgeHubGetter;
        readonly Task<IHttpRequestAuthenticator> authenticatorGetter;

        public DeviceScopeController(Task<IEdgeHub> edgeHub, Task<IHttpRequestAuthenticator> authenticator)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.authenticatorGetter = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/devicesAndModulesInTargetDeviceScope")]
        public async Task GetDevicesAndModulesInTargetDeviceScopeAsync([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] NestedScopeRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));
            Preconditions.CheckNonWhiteSpace(request.AuthChain, nameof(request.AuthChain));

            if (actorModuleId != Constants.EdgeHubModuleId)
            {
                // Only child EdgeHubs are allowed to act OnBehalfOf of devices/modules.
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId));
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }

            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(actorModuleId), Option.Some(request.AuthChain), this.HttpContext);

            if (authResult.Authenticated)
            {
                EdgeHubScopeResult reqResult = await this.HandleDevicesAndModulesInTargetDeviceScopeAsync(actorDeviceId, actorModuleId, request);
                await this.SendResponse(reqResult.Status, JsonConvert.SerializeObject(reqResult));
            }
            else
            {
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, authResult.ErrorMessage);
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/getDeviceAndModuleOnBehalfOf")]
        public async Task GetDeviceAndModuleOnBehalfOfAsync([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] IdentityOnBehalfOfRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));
            Preconditions.CheckNonWhiteSpace(request.AuthChain, nameof(request.AuthChain));

            if (actorModuleId != Constants.EdgeHubModuleId)
            {
                // Only child EdgeHubs are allowed to act OnBehalfOf of devices/modules.
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId));
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }

            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(actorModuleId), Option.Some(request.AuthChain), this.HttpContext);

            if (authResult.Authenticated)
            {
                EdgeHubScopeResult reqResult = await this.HandleGetDeviceAndModuleOnBehalfOfAsync(actorDeviceId, actorModuleId, request);
                await this.SendResponse(reqResult.Status, JsonConvert.SerializeObject(reqResult));
            }
            else
            {
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, authResult.ErrorMessage);
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }
        }

        async Task<EdgeHubScopeResult> HandleDevicesAndModulesInTargetDeviceScopeAsync(string actorDeviceId, string actorModuleId, NestedScopeRequest request)
        {
            Events.ReceivedScopeRequest(actorDeviceId, actorModuleId, request);

            if (!this.TryGetTargetDeviceId(request.AuthChain, out string targetDeviceId))
            {
                return new EdgeHubScopeResultError(HttpStatusCode.BadRequest, Events.InvalidRequestAuthchain(request.AuthChain));
            }

            // Get the children of the target device and the target device itself;
            IEdgeHub edgeHub = await this.edgeHubGetter;
            IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
            IList<ServiceIdentity> identities = await identitiesCache.GetDevicesAndModulesInTargetScopeAsync(targetDeviceId);
            Option<ServiceIdentity> targetDevice = await identitiesCache.GetServiceIdentity(targetDeviceId);
            targetDevice.ForEach(d => identities.Add(d));

            // Construct the result from the identities
            Events.SendingScopeResult(targetDeviceId, identities);
            return MakeResultFromIdentities(identities);
        }

        async Task<EdgeHubScopeResult> HandleGetDeviceAndModuleOnBehalfOfAsync(string actorDeviceId, string actorModuleId, IdentityOnBehalfOfRequest request)
        {
            Events.ReceivedIdentityOnBehalfOfRequest(actorDeviceId, actorModuleId, request);
            Preconditions.CheckNonWhiteSpace(request.TargetDeviceId, nameof(request.TargetDeviceId));

            bool isModule = false;
            string targetId = request.TargetDeviceId;
            if (!request.TargetModuleId.IsNullOrWhiteSpace())
            {
                isModule = true;
                targetId += "/" + request.TargetModuleId;
            }

            IEdgeHub edgeHub = await this.edgeHubGetter;

            // Check if the actor has provided the originating EdgeHub ID,
            // if not, then we must be by definition by the origin.
            string originatingEdge = edgeHub.GetEdgeDeviceId();
            if (this.Request.Headers.TryGetValue(Constants.OriginEdgeHeaderKey, out StringValues originHeader) && originHeader.Count > 0)
            {
                originatingEdge = originHeader.First();
            }

            // We must always forward the call further upstream first,
            // as this is invoked for refreshing an identity on-demand,
            // and we don't know whether our cache is out-of-date.
            IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
            await identitiesCache.RefreshServiceIdentityOnBehalfOf(targetId, originatingEdge);
            Option<ServiceIdentity> targetIdentity = await identitiesCache.GetServiceIdentity(targetId);

            if (!targetIdentity.HasValue)
            {
                // Identity still doesn't exist, this can happen if the identity
                // is newly added and we couldn't refresh the individual identity
                // because we don't know where it resides in the nested hierarchy.
                // In this case our only recourse is to refresh the whole cache
                // and hope the identity shows up.
                identitiesCache.InitiateCacheRefresh();
                await identitiesCache.WaitForCacheRefresh(TimeSpan.FromSeconds(100));
                targetIdentity = await identitiesCache.GetServiceIdentity(targetId);
            }

            // Add the identity to the result
            var identityList = new List<ServiceIdentity>();
            targetIdentity.ForEach(i => identityList.Add(i));

            // If the target is a module, we also need to
            // include the parent device as well to match
            // IoT Hub API behavior
            if (isModule)
            {
                Option<ServiceIdentity> device = await identitiesCache.GetServiceIdentity(request.TargetDeviceId);
                device.ForEach(i => identityList.Add(i));
            }

            Events.SendingScopeResult(targetId, identityList);
            return MakeResultFromIdentities(identityList);
        }

        bool IsRefreshIdentityNeeded(Option<ServiceIdentity> identityOption)
        {
            // Default refresh to true if we don't have the identity yet.
            bool needToRefresh = true;

            identityOption.ForEach(id =>
            {
                // Identities can initially be created with no auth, and
                // have their auth type updated later. In this case we
                // must refresh the identity or we won't be able to auth
                // incoming OnBehalfOf connections for those identities.
                needToRefresh = id.Authentication.Type == ServiceAuthenticationType.None;
            });

            return needToRefresh;
        }

        bool TryGetTargetDeviceId(string authChain, out string targetDeviceId)
        {
            targetDeviceId = string.Empty;

            // The target device is the first ID in the provided authchain,
            // which could be a module ID of the format "deviceId/moduleId".
            var actorAuthChainIds = authChain.Split(';', StringSplitOptions.RemoveEmptyEntries);

            if (actorAuthChainIds.Length > 0)
            {
                var ids = actorAuthChainIds[0].Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (ids.Length > 0)
                {
                    targetDeviceId = ids[0];
                    return true;
                }
            }

            return false;
        }

        async Task SendResponse(HttpStatusCode status, string responseJson)
        {
            this.Response.StatusCode = (int)status;
            var resultUtf8Bytes = Encoding.UTF8.GetBytes(responseJson);

            this.Response.ContentLength = resultUtf8Bytes.Length;
            this.Response.ContentType = SupportedContentType;

            await this.Response.Body.WriteAsync(resultUtf8Bytes, 0, resultUtf8Bytes.Length);
        }

        static EdgeHubScopeResult MakeResultFromIdentities(IList<ServiceIdentity> identities)
        {
            var result = new EdgeHubScopeResultSuccess();

            foreach (ServiceIdentity identity in identities)
            {
                if (identity.IsModule)
                {
                    result.Modules.Add(identity.ToEdgeHubScopeModule());
                }
                else
                {
                    result.Devices.Add(identity.ToEdgeHubScopeDevice());
                }
            }

            return result;
        }

        static class Events
        {
            const int IdStart = HttpEventIds.DeviceScopeController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeController>();

            enum EventIds
            {
                ReceivedScopeRequest = IdStart,
                ReceivedIdentityOnBehalfOfRequest,
                SendingScopeResult,
                UnauthorizedActor,
                InvalidRequestAuthchain,
                AuthFail_NoHeader,
                AuthFail_BadHeader,
                AuthFail_ActorMismatch,
                AuthFail_InvalidAuthChain
            }

            public static void ReceivedScopeRequest(string actorDeviceId, string actorModuleId, NestedScopeRequest request)
            {
                Log.LogInformation((int)EventIds.ReceivedScopeRequest, $"Received get scope request: actorId: {actorDeviceId}/{actorModuleId}, authChain: {request.AuthChain}, continuationLink: {request.ContinuationLink}, pageSize: {request.PageSize}");
            }

            public static void ReceivedIdentityOnBehalfOfRequest(string actorDeviceId, string actorModuleId, IdentityOnBehalfOfRequest request)
            {
                Log.LogInformation((int)EventIds.ReceivedIdentityOnBehalfOfRequest, $"Received get scope request: actorId: {actorDeviceId}/{actorModuleId}, authChain: {request.AuthChain}, targetDevice: {request.TargetDeviceId}, targetModule: {request.TargetModuleId}");
            }

            public static void SendingScopeResult(string targetId, IList<ServiceIdentity> identities)
            {
                Log.LogInformation((int)EventIds.SendingScopeResult, $"Sending ScopeResult for {targetId}: [{string.Join(", ", identities.Select(identity => identity.Id))}]");
            }

            public static string UnauthorizedActor(string actorDeviceId, string actorModuleId)
            {
                string msg = $"{actorDeviceId}/{actorModuleId} not authorized to establish OnBehalfOf connection";
                Log.LogError((int)EventIds.UnauthorizedActor, msg);
                return msg;
            }

            public static string InvalidRequestAuthchain(string authChain)
            {
                string msg = $"Invalid auth chain: {authChain}";
                Log.LogError((int)EventIds.InvalidRequestAuthchain, msg);
                return msg;
            }

            public static void AuthFail_InvalidAuthChain(string actorId, string targetId, string authChain)
            {
                Log.LogError((int)EventIds.AuthFail_InvalidAuthChain, $"Invalid auth chain, actor: {actorId}, target: {targetId}, auth chain: {authChain}");
            }
        }
    }
}
