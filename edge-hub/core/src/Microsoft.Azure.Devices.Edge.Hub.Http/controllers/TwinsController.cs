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
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class TwinsController : Controller
    {
        static readonly string supportedContentType = "application/json; charset=utf-8";

        readonly Task<IEdgeHub> edgeHubGetter;
        readonly Task<IHttpRequestAuthenticator> authenticatorGetter;
        readonly IValidator<MethodRequest> validator;

        public TwinsController(Task<IEdgeHub> edgeHub, Task<IHttpRequestAuthenticator> authenticator, IValidator<MethodRequest> validator)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.authenticatorGetter = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.validator = Preconditions.CheckNotNull(validator, nameof(validator));
        }

        [HttpPost]
        [Route("twins/{deviceId}/methods")]
        public async Task InvokeDeviceMethodAsync([FromRoute] string deviceId, [FromBody] MethodRequest methodRequest)
        {
            deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            this.validator.Validate(methodRequest);

            var directMethodRequest = new DirectMethodRequest(deviceId, methodRequest.MethodName, methodRequest.PayloadBytes, methodRequest.ResponseTimeout, methodRequest.ConnectTimeout);
            var methodResult = await this.InvokeMethodAsync(directMethodRequest);
            await this.SendResponse(methodResult);
        }

        [HttpPost]
        [Route("twins/{deviceId}/modules/{moduleId}/methods")]
        public async Task InvokeModuleMethodAsync([FromRoute] string deviceId, [FromRoute] string moduleId, [FromBody] MethodRequest methodRequest)
        {
            deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
            this.validator.Validate(methodRequest);

            var directMethodRequest = new DirectMethodRequest($"{deviceId}/{moduleId}", methodRequest.MethodName, methodRequest.PayloadBytes, methodRequest.ResponseTimeout, methodRequest.ConnectTimeout);
            var methodResult = await this.InvokeMethodAsync(directMethodRequest);
            await this.SendResponse(methodResult);
        }

        bool TryGetActorId(out string deviceId, out string moduleId)
        {
            deviceId = string.Empty;
            moduleId = string.Empty;

            if (!this.Request.Headers.TryGetValue(Constants.ServiceApiIdHeaderKey, out StringValues clientIds) || clientIds.Count == 0)
            {
                // Must have valid header to specify actor
                return false;
            }

            string clientId = clientIds.First();
            string[] clientIdParts = clientId.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (clientIdParts.Length != 2)
            {
                // Actor should always be <deviceId>/<moduleId>
                return false;
            }

            deviceId = clientIdParts[0];
            moduleId = clientIdParts[1];
            return true;
        }

        internal static MethodResult GetMethodResult(DirectMethodResponse directMethodResponse) =>
            directMethodResponse.Exception.Map(e => new MethodErrorResult(directMethodResponse.HttpStatusCode, e.Message) as MethodResult)
                .GetOrElse(() => new MethodSuccessResult(directMethodResponse.Status, GetRawJson(directMethodResponse.Data)));

        internal static JRaw GetRawJson(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(bytes);
            return new JRaw(json);
        }

        async Task<MethodResult> InvokeMethodAsync(DirectMethodRequest directMethodRequest)
        {
            Events.ReceivedMethodCall(directMethodRequest);
            IEdgeHub edgeHub = await this.edgeHubGetter;

            MethodResult methodResult;
            string currentEdgeDeviceId = edgeHub.GetEdgeDeviceId();

            if (this.TryGetActorId(out string actorDeviceId, out string actorModuleId))
            {
                string actorId = $"{actorDeviceId}/{actorModuleId}";

                if (actorDeviceId == currentEdgeDeviceId)
                {
                    IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
                    HttpAuthResult authResult = await authenticator.AuthenticateAsync(actorDeviceId, Option.Some(actorModuleId), Option.None<string>(), this.HttpContext);

                    if (authResult.Authenticated)
                    {
                        using (Metrics.TimeDirectMethod(actorDeviceId, directMethodRequest.Id))
                        {
                            DirectMethodResponse directMethodResponse = await edgeHub.InvokeMethodAsync(actorId, directMethodRequest);
                            Events.ReceivedMethodCallResponse(directMethodRequest, actorId);

                            methodResult = GetMethodResult(directMethodResponse);
                        }
                    }
                    else
                    {
                        methodResult = new MethodErrorResult(HttpStatusCode.Unauthorized, authResult.ErrorMessage);
                    }
                }
                else
                {
                    methodResult = new MethodErrorResult(HttpStatusCode.Unauthorized, "Only modules on the same device can invoke DirectMethods");
                }
            }
            else
            {
                methodResult = new MethodErrorResult(HttpStatusCode.BadRequest, $"Invalid header value for {Constants.ServiceApiIdHeaderKey}");
            }

            return methodResult;
        }

        async Task SendResponse(MethodResult methodResult)
        {
            var resultJsonContent = JsonConvert.SerializeObject(methodResult);
            var resultUtf8Bytes = Encoding.UTF8.GetBytes(resultJsonContent);

            this.Response.ContentLength = resultUtf8Bytes.Length;
            this.Response.ContentType = supportedContentType;
            this.Response.StatusCode = (int)methodResult.StatusCode;

            await this.Response.Body.WriteAsync(resultUtf8Bytes, 0, resultUtf8Bytes.Length);
        }

        static class Events
        {
            const int IdStart = HttpEventIds.TwinsController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinsController>();

            enum EventIds
            {
                ReceivedMethodCall = IdStart,
                ReceivedMethodResponse
            }

            public static void ReceivedMethodCall(DirectMethodRequest methodRequest)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodCall, $"Received call to invoke method {methodRequest.Name} on device or module {methodRequest.Id}");
            }

            public static void ReceivedMethodCallResponse(DirectMethodRequest methodRequest, string actorId)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodResponse, $"Received response from call to method {methodRequest.Name} from device or module {methodRequest.Id}. Method invoked by module {actorId}");
            }
        }

        static class Metrics
        {
            static readonly IMetricsTimer DirectMethodsTimer = Util.Metrics.Metrics.Instance.CreateTimer(
                "direct_method_duration_seconds",
                "Time taken to call direct method",
                new List<string> { "from", "to" });

            public static IDisposable TimeDirectMethod(string fromId, string toId) => DirectMethodsTimer.GetTimer(new[] { fromId, toId });
        }
    }
}
