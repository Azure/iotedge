// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Newtonsoft.Json;

    public class EdgeHubScopeResult
    {
        [JsonIgnore]
        public HttpStatusCode Status { get; set; }

        [JsonProperty(PropertyName = "devices", Required = Required.AllowNull)]
        public IEnumerable<EdgeHubScopeDevice> Devices { get; set; }

        [JsonProperty(PropertyName = "modules", Required = Required.AllowNull)]
        public IEnumerable<EdgeHubScopeModule> Modules { get; set; }
    }
}
