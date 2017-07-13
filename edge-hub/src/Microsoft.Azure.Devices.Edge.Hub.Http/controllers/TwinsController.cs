
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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
        [Route("twins/{id}/methods")]
        public async Task<IActionResult> InvokeDeviceMethodAsync([FromRoute] string id, [FromBody] MethodRequest methodRequest)
        {
            id = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(id, nameof(id)));
            this.validator.Validate(methodRequest);

            Events.ReceivedMethodCall(id, methodRequest, this.identity);
            var directMethodRequest = new DirectMethodRequest(id, methodRequest.MethodName, methodRequest.PayloadBytes, methodRequest.ResponseTimeout, methodRequest.ConnectTimeout);
            IEdgeHub edgeHub = await this.edgeHubGetter;
            DirectMethodResponse directMethodResponse = await edgeHub.InvokeMethodAsync(this.identity, directMethodRequest);
            Events.ReceivedMethodCallResponse(id, methodRequest, this.identity);

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

            public static void ReceivedMethodCall(string id, MethodRequest methodRequest, IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodCall, $"Received call to invoke method {methodRequest.MethodName} on device/module {id} from module {identity.Id}");
            }

            public static void ReceivedMethodCallResponse(string id, MethodRequest methodRequest, IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ReceivedMethodResponse, $"Received response from method {methodRequest.MethodName} on device/module {id} for module {identity.Id}");
            }
        }
    }
}
