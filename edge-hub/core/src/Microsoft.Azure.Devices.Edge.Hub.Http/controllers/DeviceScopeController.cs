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

        public DeviceScopeController(Task<IEdgeHub> edgeHub)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/devicesAndModulesInTargetDeviceScope")]
        public async Task GetDevicesAndModulesInTargetDeviceScope([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] NestedScopeRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));
            Preconditions.CheckArgument(actorModuleId == Constants.EdgeHubModuleId);

            EdgeHubScopeResult result = await this.HandleDevicesAndModulesInTargetDeviceScope(actorDeviceId, request);
            await this.SendResponse(result);
        }

        [HttpPost]
        [Route("devices/{actorDeviceId}/modules/{actorModuleId}/getDeviceAndModuleOnBehalfOf")]
        public async Task GetDeviceAndModuleOnBehalfOf([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] IdentityOnBehalfOfRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));
            Preconditions.CheckArgument(actorModuleId == Constants.EdgeHubModuleId);

            EdgeHubScopeResult result = await this.HandleGetDeviceAndModuleOnBehalfOf(actorDeviceId, request);
            await this.SendResponse(result);
        }

        async Task<EdgeHubScopeResult> HandleDevicesAndModulesInTargetDeviceScope(string actorDeviceId, NestedScopeRequest request)
        {
            Events.ReceivedScopeRequest(actorDeviceId, request);
            Preconditions.CheckNonWhiteSpace(request.AuthChain, nameof(request.AuthChain));

            EdgeHubScopeResult result = new EdgeHubScopeResult();

            if (!this.TryGetTargetDeviceId(request.AuthChain, out string targetDeviceId))
            {
                Events.InvalidAuthchain(request.AuthChain);
                result.Status = HttpStatusCode.BadRequest;
                return result;
            }

            // Check that the actor device is authorized to act OnBehalfOf the target
            IEdgeHub edgeHub = await this.edgeHubGetter;
            if (!await this.AuthorizeActorDevice(edgeHub, actorDeviceId, targetDeviceId))
            {
                Events.UnauthorizedActor(actorDeviceId, targetDeviceId);
                result.Status = HttpStatusCode.Unauthorized;
                return result;
            }

            // Construct the result
            IList<ServiceIdentity> identities = await edgeHub.GetDevicesAndModulesInTargetScopeAsync(targetDeviceId);
            Events.SendingScopeResult(identities);
            result = MakeResultFromIdentities(identities);
            result.Status = HttpStatusCode.OK;

            return result;
        }

        async Task<EdgeHubScopeResult> HandleGetDeviceAndModuleOnBehalfOf(string actorDeviceId, IdentityOnBehalfOfRequest request)
        {
            Events.ReceivedIdentityOnBehalfOfRequest(actorDeviceId, request);
            Preconditions.CheckNonWhiteSpace(request.TargetDeviceId, nameof(request.TargetDeviceId));

            EdgeHubScopeResult result = new EdgeHubScopeResult();

            bool isModule = false;
            string targetId = request.TargetDeviceId;
            if (!request.TargetModuleId.IsNullOrWhiteSpace())
            {
                isModule = true;
                targetId += "/" + request.TargetModuleId;
            }

            // Check that the actor device is authorized to act OnBehalfOf the target
            IEdgeHub edgeHub = await this.edgeHubGetter;
            if (!await this.AuthorizeActorDevice(edgeHub, actorDeviceId, targetId))
            {
                Events.UnauthorizedActor(actorDeviceId, targetId);
                result.Status = HttpStatusCode.Unauthorized;
                return result;
            }

            // Get the target identity
            var identityList = new List<ServiceIdentity>();
            Option<ServiceIdentity> identity = await edgeHub.GetIdentityAsync(targetId);
            identity.ForEach(i => identityList.Add(i));

            // If the target is a module, we also need to
            // include the parent device as well to match
            // IoT Hub API behavior
            if (isModule)
            {
                Option<ServiceIdentity> device = await edgeHub.GetIdentityAsync(request.TargetDeviceId);
                device.ForEach(i => identityList.Add(i));
            }

            Events.SendingScopeResult(identityList);
            result = MakeResultFromIdentities(identityList);
            result.Status = HttpStatusCode.OK;

            return result;
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

        async Task<bool> AuthorizeActorDevice(IEdgeHub edgeHub, string actorDeviceId, string targetId)
        {
            // Actor device is claiming to be our child, and that the target device is its child.
            // So we should have an authchain already cached for the target device.
            Option<string> targetAuthChain = await edgeHub.GetAuthChainForIdentity(targetId);

            if (!targetAuthChain.HasValue)
            {
                return false;
            }

            // Validate the the target auth-chain
            if (!AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetId, targetAuthChain.Expect(() => new InvalidOperationException())))
            {
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
                AuthchainMismatch,
                UnauthorizedActor,
                InvalidAuthchain
            }

            public static void ReceivedScopeRequest(string actorDeviceId, NestedScopeRequest request)
            {
                Log.LogInformation((int)EventIds.ReceivedScopeRequest, $"Received get scope request: actorDevice: {actorDeviceId}, authChain: {request.AuthChain}, continuationLink: {request.ContinuationLink}, pageSize: {request.PageSize}");
            }

            public static void ReceivedIdentityOnBehalfOfRequest(string actorDeviceId, IdentityOnBehalfOfRequest request)
            {
                Log.LogInformation((int)EventIds.ReceivedIdentityOnBehalfOfRequest, $"Received get scope request: actorDevice: {actorDeviceId}, authChain: {request.AuthChain}, targetDevice: {request.TargetDeviceId}, targetModule: {request.TargetModuleId}");
            }

            public static void SendingScopeResult(IList<ServiceIdentity> identities)
            {
                Log.LogInformation((int)EventIds.SendingScopeResult, $"Sending ScopeResult: [{string.Join(", ", identities.Select(identity => identity.Id))}]");
            }

            public static void UnauthorizedActor(string actorDeviceId, string targetDeviceId)
            {
                Log.LogError((int)EventIds.UnauthorizedActor, $"{actorDeviceId} not authorized to act OnBehalfOf {targetDeviceId}");
            }

            public static void InvalidAuthchain(string authChain)
            {
                Log.LogError((int)EventIds.AuthchainMismatch, $"Invalid auth chain: {authChain}");
            }
        }
    }
}
