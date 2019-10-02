// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class TwinsController : Controller
    {
        static readonly string supportedContentType = "application/json; charset=utf-8";

        readonly Task<IEdgeHub> edgeHubGetter;
        readonly IValidator<MethodRequest> validator;
        IIdentity identity;

        public TwinsController(Task<IEdgeHub> edgeHub, IValidator<MethodRequest> validator)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.validator = Preconditions.CheckNotNull(validator, nameof(validator));
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Items.TryGetValue(HttpConstants.IdentityKey, out object contextIdentity))
            {
                this.identity = contextIdentity as IIdentity;
            }

            base.OnActionExecuting(context);
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
            Events.ReceivedMethodCall(directMethodRequest, this.identity);
            IEdgeHub edgeHub = await this.edgeHubGetter;
            DirectMethodResponse directMethodResponse = await edgeHub.InvokeMethodAsync(this.identity.Id, directMethodRequest);
            Events.ReceivedMethodCallResponse(directMethodRequest, this.identity);

            MethodResult methodResult = GetMethodResult(directMethodResponse);

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

            public static void ReceivedMethodCall(DirectMethodRequest methodRequest, IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodCall, $"Received call to invoke method {methodRequest.Name} on device or module {methodRequest.Id} from module {identity.Id}");
            }

            public static void ReceivedMethodCallResponse(DirectMethodRequest methodRequest, IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodResponse, $"Received response from call to method {methodRequest.Name} from device or module {methodRequest.Id}. Method invoked by module {identity.Id}");
            }
        }
    }
}
