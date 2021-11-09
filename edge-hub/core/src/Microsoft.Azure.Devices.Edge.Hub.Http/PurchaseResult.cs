// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net;
    using Newtonsoft.Json;

    public abstract class PurchaseResult
    {
        protected PurchaseResult(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        [JsonIgnore]
        public HttpStatusCode StatusCode { get; }
    }

    public class PurchaseResultSuccess : PurchaseResult
    {
        public PurchaseResultSuccess() : base(HttpStatusCode.OK)
        {
        }

        [JsonProperty("publisherId")]
        public string PublisherId { get; set; }

        [JsonProperty("offerId")]
        public string OfferId { get; set; }

        [JsonProperty("planId")]
        public string PlanId { get; set; }

        [JsonProperty("purchaseStatus")]
        public PurchaseStatus PurchaseStatus { get; set; }
    }

    public class PurchaseResultError : PurchaseResult
    {
        public PurchaseResultError(HttpStatusCode status, string errorMessage)
            : base(status)
        {
            this.ErrorMessage = errorMessage;
        }

        [JsonProperty("ErrorMessage")]
        public string ErrorMessage { get; set; }
    }

    public enum PurchaseStatus
    {
        NotFound,
        Complete
    }
}
