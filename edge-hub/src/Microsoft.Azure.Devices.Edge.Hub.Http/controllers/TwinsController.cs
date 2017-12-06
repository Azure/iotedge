
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

    public class TwinsController : Controller
    {
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
        public Task<IActionResult> InvokeDeviceMethodAsync([FromRoute] string deviceId, [FromBody] MethodRequest methodRequest)
        {
            deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            this.validator.Validate(methodRequest);

            var directMethodRequest = new DirectMethodRequest(deviceId, methodRequest.MethodName, methodRequest.PayloadBytes, methodRequest.ResponseTimeout, methodRequest.ConnectTimeout);
            return this.InvokeMethodAsync(directMethodRequest);
        }

        [HttpPost]
        [Route("twins/{deviceId}/modules/{moduleId}/methods")]
        public Task<IActionResult> InvokeModuleMethodAsync([FromRoute] string deviceId, [FromRoute] string moduleId, [FromBody] MethodRequest methodRequest)
        {
            deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));
            this.validator.Validate(methodRequest);

            var directMethodRequest = new DirectMethodRequest($"{deviceId}/{moduleId}", methodRequest.MethodName, methodRequest.PayloadBytes, methodRequest.ResponseTimeout, methodRequest.ConnectTimeout);
            return this.InvokeMethodAsync(directMethodRequest);
        }

        async Task<IActionResult> InvokeMethodAsync(DirectMethodRequest directMethodRequest)
        {
            Events.ReceivedMethodCall(directMethodRequest, this.identity);
            IEdgeHub edgeHub = await this.edgeHubGetter;
            DirectMethodResponse directMethodResponse = await edgeHub.InvokeMethodAsync(this.identity, directMethodRequest);
            Events.ReceivedMethodCallResponse(directMethodRequest, this.identity);

            var methodResult = new MethodResult
            {
                Status = directMethodResponse.Status,
                Payload = directMethodResponse.Data != null ? Encoding.UTF8.GetString(directMethodResponse.Data) : string.Empty
            };
            return this.Json(methodResult);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinsController>();
            const int IdStart = HttpEventIds.TwinsController;

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
