// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Billing
{
    using System;
    using Newtonsoft.Json;

    public class PurchaseContent : IEquatable<PurchaseContent>
    {
        [JsonProperty("publisherId")]
        public string PublisherId { get; set; }

        [JsonProperty("offerId")]
        public string OfferId { get; set; }

        [JsonProperty("planId")]
        public string PlanId { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((PurchaseContent)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.PublisherId != null ? this.PublisherId.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.OfferId != null ? this.OfferId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.PlanId != null ? this.PlanId.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool Equals(PurchaseContent other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.PublisherId, other.PublisherId)
                && string.Equals(this.OfferId, other.OfferId)
                && string.Equals(this.PlanId, other.PlanId);
        }
    }
}
