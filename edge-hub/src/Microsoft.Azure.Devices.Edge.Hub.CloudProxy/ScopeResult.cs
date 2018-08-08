// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ScopeResult
    {
        /// <summary>
        /// The scope result items, as a collection.
        /// </summary>
        [JsonProperty(PropertyName = "devices", Required = Required.AllowNull)]
        public IEnumerable<Device> Devices { get; set; }

        /// <summary>
        /// The scope result items, as a collection.
        /// </summary>
        [JsonProperty(PropertyName = "modules", Required = Required.AllowNull)]
        public IEnumerable<Module> Modules { get; set; }

        /// <summary>
        /// Request continuation token.
        /// </summary>
        [JsonProperty(PropertyName = "continuationLink", Required = Required.AllowNull)]
        public string ContinuationLink { get; set; }
    }
}
