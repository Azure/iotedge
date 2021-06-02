// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Newtonsoft.Json;

    public abstract class EdgeHubScopeResult
    {
        protected EdgeHubScopeResult(HttpStatusCode status)
        {
            this.Status = status;
        }

        [JsonIgnore]
        public HttpStatusCode Status { get; }
    }

    public class EdgeHubScopeResultSuccess : EdgeHubScopeResult
    {
        public EdgeHubScopeResultSuccess()
            : base(HttpStatusCode.OK)
        {
            this.Devices = new List<EdgeHubScopeDevice>();
            this.Modules = new List<EdgeHubScopeModule>();
        }

        public EdgeHubScopeResultSuccess(IList<EdgeHubScopeDevice> devices, IList<EdgeHubScopeModule> modules)
            : base(HttpStatusCode.OK)
        {
            this.Devices = devices;
            this.Modules = modules;
        }

        [JsonProperty(PropertyName = "devices", Required = Required.AllowNull)]
        public IList<EdgeHubScopeDevice> Devices { get; }

        [JsonProperty(PropertyName = "modules", Required = Required.AllowNull)]
        public IList<EdgeHubScopeModule> Modules { get; }

        [JsonProperty(PropertyName = "continuationLink", Required = Required.AllowNull)]
        public string ContinuationLink { get; }
    }

    public class EdgeHubScopeResultError : EdgeHubScopeResult
    {
        public EdgeHubScopeResultError(HttpStatusCode status, string errorMessage)
            : base(status)
        {
            this.ErrorMessage = errorMessage;
        }

        [JsonProperty("ErrorMessage")]
        public string ErrorMessage { get; set; }
    }
}
