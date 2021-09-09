// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This is an object to encapsulate JSON serialization of the
    /// GetDevicesAndModulesInTargetScope POST request.
    /// </summary>
    public class NestedScopeRequest
    {
        [JsonProperty(PropertyName = "pageSize")]
        public int PageSize { get; private set; }

        [JsonProperty(PropertyName = "continuationLink", NullValueHandling = NullValueHandling.Ignore)]
        public string ContinuationLink { get; private set; }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        public NestedScopeRequest(int pageSize, string continuationLink, string authChain)
        {
            this.PageSize = pageSize;
            this.ContinuationLink = continuationLink;
            this.AuthChain = Preconditions.CheckNonWhiteSpace(authChain, nameof(authChain));
        }
    }
}
