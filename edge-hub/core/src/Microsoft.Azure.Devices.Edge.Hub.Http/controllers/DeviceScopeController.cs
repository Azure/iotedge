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

        public DeviceScopeController(Task<IEdgeHub> edgeHub)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/devicesAndModulesInTargetDeviceScope")]
        public async Task GetDevicesAndModulesInTargetDeviceScopeAsync([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] NestedScopeRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));

            EdgeHubScopeResult result = await this.HandleDevicesAndModulesInTargetDeviceScopeAsync(actorDeviceId, actorModuleId, request);
            await this.SendResponse(result);
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/getDeviceAndModuleOnBehalfOf")]
        public async Task GetDeviceAndModuleOnBehalfOfAsync([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] IdentityOnBehalfOfRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));

            EdgeHubScopeResult result = await this.HandleGetDeviceAndModuleOnBehalfOfAsync(actorDeviceId, actorModuleId, request);
            await this.SendResponse(result);
        }

        async Task<EdgeHubScopeResult> HandleDevicesAndModulesInTargetDeviceScopeAsync(string actorDeviceId, string actorModuleId, NestedScopeRequest request)
        {
            Events.ReceivedScopeRequest(actorDeviceId, actorModuleId, request);
            Preconditions.CheckNonWhiteSpace(request.AuthChain, nameof(request.AuthChain));

            EdgeHubScopeResult result = new EdgeHubScopeResult();

            if (!this.TryGetTargetDeviceId(request.AuthChain, out string targetDeviceId))
            {
                Events.InvalidRequestAuthchain(request.AuthChain);
                result.Status = HttpStatusCode.BadRequest;
                return result;
            }

            // Check that the actor device is authorized to act OnBehalfOf the target
            IEdgeHub edgeHub = await this.edgeHubGetter;
            IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
            if (!await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, actorModuleId, targetDeviceId))
            {
                Events.UnauthorizedActor(actorDeviceId, actorModuleId, targetDeviceId);
                result.Status = HttpStatusCode.Unauthorized;
                return result;
            }

            // Get the children of the target device and the target device itself;
            IList<ServiceIdentity> identities = await identitiesCache.GetDevicesAndModulesInTargetScopeAsync(targetDeviceId);
            Option<ServiceIdentity> targetDevice = await identitiesCache.GetServiceIdentity(targetDeviceId);
            targetDevice.ForEach(d => identities.Add(d));

            // Construct the result from the identities
            Events.SendingScopeResult(targetDeviceId, identities);
            result = MakeResultFromIdentities(identities);
            result.Status = HttpStatusCode.OK;

            return result;
        }

        async Task<EdgeHubScopeResult> HandleGetDeviceAndModuleOnBehalfOfAsync(string actorDeviceId, string actorModuleId, IdentityOnBehalfOfRequest request)
        {
            Events.ReceivedIdentityOnBehalfOfRequest(actorDeviceId, actorModuleId, request);
            Preconditions.CheckNonWhiteSpace(request.TargetDeviceId, nameof(request.TargetDeviceId));

            EdgeHubScopeResult result = new EdgeHubScopeResult();

            bool isModule = false;
            string targetId = request.TargetDeviceId;
            if (!request.TargetModuleId.IsNullOrWhiteSpace())
            {
                isModule = true;
                targetId += "/" + request.TargetModuleId;
            }

            // Try updating our cache and get the target identity first
            IEdgeHub edgeHub = await this.edgeHubGetter;
            IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
            Option<ServiceIdentity> targetIdentity = await identitiesCache.GetServiceIdentity(targetId);

            if (this.IsRefreshIdentityNeeded(targetIdentity))
            {
                // Identity doesn't exist, this can happen if the target identity
                // is newly created in IoT Hub. In this case, we try to refresh
                // the target from upstream, which will cause any parent Edge
                // devices to refresh their respective identity cache.
                await identitiesCache.RefreshServiceIdentity(targetId);
                targetIdentity = await identitiesCache.GetServiceIdentity(targetId);

                if (this.IsRefreshIdentityNeeded(targetIdentity))
                {
                    // Identity still doesn't exist. It's possible that we're nested,
                    // so we need to refresh our identity cache to satisfy the prior
                    // logic, in case this call came from a child Edge device.
                    identitiesCache.InitiateCacheRefresh();
                    await identitiesCache.WaitForCacheRefresh(TimeSpan.FromSeconds(100));
                    targetIdentity = await identitiesCache.GetServiceIdentity(targetId);
                }
            }

            // Now that our cache is up-to-date, check that the actor device is
            // authorized to act OnBehalfOf the target
            if (!await this.AuthorizeActorAsync(identitiesCache, actorDeviceId, actorModuleId, targetId))
            {
                Events.UnauthorizedActor(actorDeviceId, actorModuleId, targetId);
                result.Status = HttpStatusCode.Unauthorized;
                return result;
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
            result = MakeResultFromIdentities(identityList);
            result.Status = HttpStatusCode.OK;

            return result;
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

        async Task<bool> AuthorizeActorAsync(IDeviceScopeIdentitiesCache identitiesCache, string actorDeviceId, string actorModuleId, string targetId)
        {
            if (actorModuleId != Constants.EdgeHubModuleId)
            {
                // Only child EdgeHubs are allowed to act OnBehalfOf of devices/modules.
                Events.AuthFail_BadActor(actorDeviceId, actorModuleId);
                return false;
            }

            if (!this.Request.Headers.TryGetValue(Constants.ServiceApiIdHeaderKey, out StringValues clientIds) || clientIds.Count == 0)
            {
                // Must have presented Edge header for AuthN earlier
                Events.AuthFail_NoHeader();
                return false;
            }

            string clientId = clientIds.First();
            string[] clientIdParts = clientId.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (clientIdParts.Length != 2)
            {
                // Edge header should have been a module
                Events.AuthFail_BadHeader(clientIds.First());
                return false;
            }

            if (actorDeviceId != clientIdParts[0] || actorModuleId != clientIdParts[1])
            {
                // Actor from request should match actor from Edge header
                Events.AuthFail_ActorMismatch(actorDeviceId, actorModuleId, clientIdParts[0], clientIdParts[1]);
                return false;
            }

            // Actor device is claiming to be our child, and that the target device is its child.
            // So we should have an authchain already cached for the target device.
            Option<string> targetAuthChainOption = await identitiesCache.GetAuthChain(targetId);

            if (!targetAuthChainOption.HasValue)
            {
                Events.AuthFail_NoAuthChain(targetId);
                return false;
            }

            // Validate the target auth-chain
            string targetAuthChain = targetAuthChainOption.Expect(() => new InvalidOperationException());
            if (!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetId, targetAuthChain))
            {
                Events.AuthFail_InvalidAuthChain(actorDeviceId, targetId, targetAuthChain);
                return false;
            }

            return true;
        }

        async Task SendResponse(EdgeHubScopeResult result)
        {
            this.Response.StatusCode = (int)result.Status;

            if (result.Status == HttpStatusCode.OK)
            {
                var resultJsonContent = JsonConvert.SerializeObject(result);
                var resultUtf8Bytes = Encoding.UTF8.GetBytes(resultJsonContent);

                this.Response.ContentLength = resultUtf8Bytes.Length;
                this.Response.ContentType = SupportedContentType;

                await this.Response.Body.WriteAsync(resultUtf8Bytes, 0, resultUtf8Bytes.Length);
            }
        }

        static EdgeHubScopeResult MakeResultFromIdentities(IList<ServiceIdentity> identities)
        {
            var devices = new List<EdgeHubScopeDevice>();
            var modules = new List<EdgeHubScopeModule>();

            foreach (ServiceIdentity identity in identities)
            {
                if (identity.IsModule)
                {
                    modules.Add(identity.ToEdgeHubScopeModule());
                }
                else
                {
                    devices.Add(identity.ToEdgeHubScopeDevice());
                }
            }

            return new EdgeHubScopeResult() { Devices = devices, Modules = modules };
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
                AuthFail_BadActor,
                AuthFail_NoHeader,
                AuthFail_BadHeader,
                AuthFail_ActorMismatch,
                AuthFail_NoAuthChain,
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

            public static void UnauthorizedActor(string actorDeviceId, string actorModuleId, string targetDeviceId)
            {
                Log.LogError((int)EventIds.UnauthorizedActor, $"{actorDeviceId}/{actorModuleId} not authorized to act OnBehalfOf {targetDeviceId}");
            }

            public static void InvalidRequestAuthchain(string authChain)
            {
                Log.LogError((int)EventIds.InvalidRequestAuthchain, $"Invalid auth chain: {authChain}");
            }

            public static void AuthFail_BadActor(string actorDeviceId, string actorModuleId)
            {
                Log.LogError((int)EventIds.AuthFail_BadActor, $"Only EdgeHub is allowed to act OnBehalfOf another identity: {actorDeviceId}/{actorModuleId}");
            }

            public static void AuthFail_NoHeader()
            {
                Log.LogError((int)EventIds.AuthFail_NoHeader, $"Missing identity header in request");
            }

            public static void AuthFail_BadHeader(string header)
            {
                Log.LogError((int)EventIds.AuthFail_BadHeader, $"Bad identity header in request: {header}");
            }

            public static void AuthFail_ActorMismatch(string uriActorDevice, string uriActorModule, string headerActorDevice, string headerActorModule)
            {
                Log.LogError((int)EventIds.AuthFail_ActorMismatch, $"Mismatched actors, uri: {uriActorDevice}/{uriActorModule}, header: {headerActorDevice}/{headerActorModule}");
            }

            public static void AuthFail_NoAuthChain(string targetId)
            {
                Log.LogError((int)EventIds.AuthFail_NoAuthChain, $"No auth chain for target identity: {targetId}");
            }

            public static void AuthFail_InvalidAuthChain(string actorId, string targetId, string authChain)
            {
                Log.LogError((int)EventIds.AuthFail_InvalidAuthChain, $"Invalid auth chain, actor: {actorId}, target: {targetId}, auth chain: {authChain}");
            }
        }
    }
}
