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
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                EdgeHubScopeResult reqResult = await HandleDevicesAndModulesInTargetDeviceScopeAsync(actorDeviceId, actorModuleId, request, identitiesCache);
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
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId, string.IsNullOrWhiteSpace(request.TargetModuleId) ? request.TargetDeviceId : $"{request.TargetDeviceId}/{request.TargetModuleId}"));
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }

            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(actorModuleId), Option.Some(request.AuthChain), this.HttpContext);

            if (authResult.Authenticated)
            {
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                EdgeHubScopeResult reqResult = await HandleGetDeviceAndModuleOnBehalfOfAsync(actorDeviceId, actorModuleId, request, identitiesCache);
                await this.SendResponse(reqResult.Status, JsonConvert.SerializeObject(reqResult));
            }
            else
            {
                var result = new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, authResult.ErrorMessage);
                await this.SendResponse(result.Status, JsonConvert.SerializeObject(result));
            }
        }

        internal static async Task<EdgeHubScopeResult> HandleDevicesAndModulesInTargetDeviceScopeAsync(string actorDeviceId, string actorModuleId, NestedScopeRequest request, IDeviceScopeIdentitiesCache identitiesCache)
        {
            Events.ReceivedScopeRequest(actorDeviceId, actorModuleId, request);

            if (!AuthChainHelpers.TryGetTargetDeviceId(request.AuthChain, out string targetDeviceId))
            {
                return new EdgeHubScopeResultError(HttpStatusCode.BadRequest, Events.InvalidRequestAuthchain(request.AuthChain));
            }

            // Get the children of the target device and the target device itself;
            Option<string> authChainToTarget = await identitiesCache.GetAuthChain(targetDeviceId);
            (bool validationResult, string errorMsg) = ValidateAuthChainForRequestor(actorDeviceId, targetDeviceId, authChainToTarget);
            if (!validationResult)
            {
                return new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, errorMsg);
            }

            IList<ServiceIdentity> identities = await identitiesCache.GetDevicesAndModulesInTargetScopeAsync(targetDeviceId);
            Option<ServiceIdentity> targetDevice = await identitiesCache.GetServiceIdentity(targetDeviceId);
            targetDevice.ForEach(d => identities.Add(d));

            // Construct the result from the identities
            Events.SendingScopeResult(targetDeviceId, identities);
            return MakeResultFromIdentities(identities);
        }

        internal static (bool result, string errorMsg) ValidateAuthChainForRequestor(string actorDeviceId, string targetDeviceId, Option<string> authChain) =>
            authChain.Match(
                ac =>
                {
                    if (!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetDeviceId, ac))
                    {
                        return (false, $"Invalid request as auth chain ({ac}) to {targetDeviceId} does not contain {actorDeviceId}");
                    }

                    return (true, string.Empty);
                },
                () =>
                {
                    Events.AuthChainToTargetNotFound(actorDeviceId, targetDeviceId);
                    return (false, $"Auth chain to target device {targetDeviceId} not found");
                });

        internal static async Task<EdgeHubScopeResult> HandleGetDeviceAndModuleOnBehalfOfAsync(string actorDeviceId, string actorModuleId, IdentityOnBehalfOfRequest request, IDeviceScopeIdentitiesCache identitiesCache)
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

            if (!AuthChainHelpers.TryGetTargetDeviceId(request.AuthChain, out string originatingEdgeDevice))
            {
                originatingEdgeDevice = actorDeviceId;
            }

            // We must always forward the call further upstream first,
            // as this is invoked for refreshing an identity on-demand,
            // and we don't know whether our cache is out-of-date.
            await identitiesCache.RefreshServiceIdentityOnBehalfOf(targetId, originatingEdgeDevice);
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
            EdgeHubScopeResult result = await targetIdentity.Match(
                async ti =>
                {
                    Option<string> authChainToTarget = await identitiesCache.GetAuthChain(targetId);

                    return await authChainToTarget.Match(
                        async ac =>
                        {
                            if (AuthChainHelpers.ValidateAuthChain(originatingEdgeDevice, targetId, ac))
                            {
                                var identityList = new List<ServiceIdentity>();
                                identityList.Add(ti);
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
                            else
                            {
                                Events.TargetNotChild(originatingEdgeDevice, targetId);
                                return new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId, targetId));
                            }
                        },
                        () =>
                        {
                            Events.AuthChainToTargetNotFound(originatingEdgeDevice, targetId);
                            return Task.FromResult(new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId, targetId)) as EdgeHubScopeResult);
                        });
                },
                () =>
                {
                    Events.TargetIdentityNotFound(originatingEdgeDevice, targetId);
                    return Task.FromResult(new EdgeHubScopeResultError(HttpStatusCode.Unauthorized, Events.UnauthorizedActor(actorDeviceId, actorModuleId, targetId)) as EdgeHubScopeResult);
                });
            return result;
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
                AuthFail_InvalidAuthChain,
                AuthFail_InvalidRequest
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

            public static string UnauthorizedActor(string actorDeviceId, string actorModuleId, string targetId)
            {
                string msg = $"{actorDeviceId}/{actorModuleId} not authorized to get information for {targetId}";
                Log.LogError((int)EventIds.UnauthorizedActor, msg);
                return msg;
            }

            internal static string UnauthorizedActor(string actorDeviceId, string actorModuleId)
            {
                string msg = $"{actorDeviceId}/{actorModuleId} not authorized to perform on-behalf-of oprations";
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

            internal static void TargetNotChild(string originatingEdgeDevice, string targetId)
            {
                Log.LogError((int)EventIds.AuthFail_InvalidRequest, $"Request to get device is invalid as {targetId} is not a child of {originatingEdgeDevice}.");
            }

            internal static void AuthChainToTargetNotFound(string originatingEdgeDevice, string targetId)
            {
                Log.LogError((int)EventIds.AuthFail_InvalidRequest, $"Request to get device {targetId} by {originatingEdgeDevice} as auth chain to {targetId} was not found.");
            }

            internal static void TargetIdentityNotFound(string originatingEdgeDevice, string targetId)
            {
                Log.LogError((int)EventIds.AuthFail_InvalidRequest, $"Device {targetId} requested by {originatingEdgeDevice} was not found in the identities cache.");
            }
        }
    }
}
