// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Billing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class BillingController : Controller
    {
        static readonly string SupportedContentType = "application/json; charset=utf-8";

        readonly Task<IEdgeHub> edgeHubGetter;
        readonly Task<IHttpRequestAuthenticator> authenticatorGetter;

        public BillingController(Task<IEdgeHub> edgeHub, Task<IHttpRequestAuthenticator> authenticator)
        {
            this.edgeHubGetter = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.authenticatorGetter = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        [HttpGet]
        [Route("devices/{deviceId}/modules/{moduleId}/purchase")]
        public async Task GetPurchaseAsync([FromRoute] string deviceId, [FromRoute] string moduleId)
        {
            deviceId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)));
            moduleId = WebUtility.UrlDecode(Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)));

            IHttpRequestAuthenticator authenticator = await this.authenticatorGetter;
            HttpAuthResult authResult = await authenticator.AuthenticateAsync(deviceId, Option.Some(moduleId), Option.None<string>(), this.HttpContext);

            if (authResult.Authenticated)
            {
                IEdgeHub edgeHub = await this.edgeHubGetter;
                IDeviceScopeIdentitiesCache identitiesCache = edgeHub.GetDeviceScopeIdentitiesCache();
                PurchaseResult reqResult = await this.HandleGetPurchaseAsync(deviceId, moduleId, identitiesCache);
                await this.SendResponse(reqResult.StatusCode, JsonConvert.SerializeObject(reqResult));
            }
            else
            {
                var result = new PurchaseResultError(HttpStatusCode.Unauthorized, authResult.ErrorMessage);
                await this.SendResponse(result.StatusCode, JsonConvert.SerializeObject(result));
            }
        }

        async Task<PurchaseResult> HandleGetPurchaseAsync(string deviceId, string moduleId, IDeviceScopeIdentitiesCache identitiesCache)
        {
            SynchedPurchase purchase = await identitiesCache.GetPurchaseAsync(deviceId, moduleId);

            Events.SendingPurchaseResult(deviceId, moduleId, purchase.PurchaseContent);
            return purchase.PurchaseContent.Match(
               p => new PurchaseResultSuccess() { PublisherId = p.PublisherId, OfferId = p.OfferId, PlanId = p.PlanId, PurchaseStatus = PurchaseStatus.Complete, SynchedDateTimeUtc = purchase.SynchedDateUtc },
               () => new PurchaseResultSuccess() { PurchaseStatus = PurchaseStatus.NotFound, SynchedDateTimeUtc = purchase.SynchedDateUtc });
        }

        async Task SendResponse(HttpStatusCode status, string responseJson)
        {
            this.Response.StatusCode = (int)status;
            var resultUtf8Bytes = Encoding.UTF8.GetBytes(responseJson);

            this.Response.ContentLength = resultUtf8Bytes.Length;
            this.Response.ContentType = SupportedContentType;

            await this.Response.Body.WriteAsync(resultUtf8Bytes, 0, resultUtf8Bytes.Length);
        }

        static class Events
        {
            const int IdStart = HttpEventIds.BillingController;
            static readonly ILogger Log = Logger.Factory.CreateLogger<BillingController>();

            enum EventIds
            {
                SendingPurchaseResult = IdStart,
            }

            public static void SendingPurchaseResult(string deviceId, string moduleId, Option<PurchaseContent> purchase)
            {
                Log.LogInformation((int)EventIds.SendingPurchaseResult, $"Sending ScopeResult for {deviceId}/{moduleId}: {purchase}");
            }
        }
    }
}
