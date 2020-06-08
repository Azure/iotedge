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
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Security;

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
        public async Task InvokeAsync([FromRoute] string actorDeviceId, [FromRoute] string actorModuleId, [FromBody] NestedScopeRequest request)
        {
            actorDeviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId)));
            actorModuleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(actorModuleId, nameof(actorModuleId)));
            Preconditions.CheckArgument(actorModuleId == Constants.EdgeHubModuleId);

            EdgeHubScopeResult result = await this.HandleDevicesAndModulesInTargetDeviceScope(actorDeviceId, actorModuleId, request);
            await this.SendResponse(result);
        }

        public async Task<EdgeHubScopeResult> HandleDevicesAndModulesInTargetDeviceScope(string actorDeviceId, string actorModuleId, NestedScopeRequest request)
        {
            Events.ReceivedScopeRequest(actorDeviceId, request);

            EdgeHubScopeResult result = new EdgeHubScopeResult();

            // Parse the target device ID from the authchain
            if (!ValidateChainAndGetTargetDeviceId(actorDeviceId, request.AuthChain, out string targetDeviceId))
            {
                Events.InvalidAuthchain(request.AuthChain);
                result.Status = HttpStatusCode.BadRequest;
                return result;
            }

            // Actor device is claiming to be our child, and that the target device is its child.
            // So we should have an authchain already cached for the target device.
            IEdgeHub edgeHub = await this.edgeHubGetter;
            Option<string> targetAuthChainOption = await edgeHub.GetAuthChainForIdentity(targetDeviceId);
            string targetAuthChain = targetAuthChainOption.Expect(() => new UnauthorizedAccessException($"{targetDeviceId} is not a child of this Edge device"));

            // The actor device should be somewhere in the target device's authchain
            if (!targetAuthChain.Contains(actorDeviceId))
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

        public static bool ValidateChainAndGetTargetDeviceId(string actorDeviceId, string authChain, out string targetDeviceId)
        {
            targetDeviceId = string.Empty;
            var actorAuthChainIds = authChain.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Should have at least 1 element in the chain
            if (actorAuthChainIds.Count < 1)
            {
                return false;
            }

            // The actor device is the one that sent the request, so the last element
            // of the auth-chain should always be the actor device's ID
            if (!actorDeviceId.Equals(actorAuthChainIds.LastOrDefault()))
            {
                return false;
            }

            // The target device ID is the first device ID in the chain
            if (!actorAuthChainIds[0].Contains('/'))
            {
                targetDeviceId = actorAuthChainIds[0];
            }
            else
            {
                // The first element is a module ID, so the target device
                // should be the second element in the chain
                if (actorAuthChainIds.Count < 2 || actorAuthChainIds[1].Contains('/'))
                {
                    // The second element must be present and be a device
                    return false;
                }

                targetDeviceId = actorAuthChainIds[1];
            }

            return true;
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
                SendingScopeResult,
                AuthchainMismatch,
                UnauthorizedActor,
                InvalidAuthchain
            }

            public static void ReceivedScopeRequest(string actorDeviceId, NestedScopeRequest request)
            {
                Log.LogInformation((int)EventIds.ReceivedScopeRequest, $"Received get scope request: actorDevice: {actorDeviceId}, authChain: {request.AuthChain}, continuationLink: {request.ContinuationLink}, pageSize: {request.PageSize}");
            }

            public static void SendingScopeResult(IList<ServiceIdentity> identities)
            {
                Log.LogInformation((int)EventIds.SendingScopeResult, $"Sending ScopeResult: {string.Join(", ", identities.Select(identity => identity.Id))}");
            }

            public static void UnauthorizedActor(string actorDeviceId, string targetDeviceId)
            {
                Log.LogError((int)EventIds.UnauthorizedActor, $"{actorDeviceId} not authorized to call OnBehalfOf {targetDeviceId}");
            }

            public static void InvalidAuthchain(string authChain)
            {
                Log.LogError((int)EventIds.AuthchainMismatch, $"Invalid auth chain: {authChain}");
            }
        }
    }
}
