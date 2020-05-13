// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Newtonsoft.Json;

    class NestedScopeRequest
    {
        [JsonProperty(PropertyName = "pageSize")]
        public int PageSize { get; set; }

        [JsonProperty(PropertyName = "continuationLink", NullValueHandling = NullValueHandling.Ignore)]
        public string ContinuationLink;

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; set; }
    }
}
