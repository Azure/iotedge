// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Billing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

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
        public PurchaseResultSuccess()
            : base(HttpStatusCode.OK)
        {
        }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("purchaseStatus")]
        public PurchaseStatus PurchaseStatus { get; set; }

        [JsonProperty("publisherId")]
        public string PublisherId { get; set; }

        [JsonProperty("offerId")]
        public string OfferId { get; set; }

        [JsonProperty("planId")]
        public string PlanId { get; set; }

        [JsonProperty("synchedDateTimeUtc")]
        public DateTime SynchedDateTimeUtc { get; set; }
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
}
