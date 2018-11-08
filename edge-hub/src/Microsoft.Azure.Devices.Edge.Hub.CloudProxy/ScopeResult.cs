// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class ScopeResult
    {
        [JsonConstructor]
        public ScopeResult(IEnumerable<Device> devices, IEnumerable<Module> modules, string continuationLink)
        {
            this.Devices = devices;
            this.Modules = modules;
            this.ContinuationLink = continuationLink;
        }

        /// <summary>
        /// Gets request continuation token.
        /// </summary>
        [JsonProperty(PropertyName = "continuationLink", Required = Required.AllowNull)]
        public string ContinuationLink { get; }

        /// <summary>
        /// Gets the scope result items, as a collection.
        /// </summary>
        [JsonProperty(PropertyName = "devices", Required = Required.AllowNull)]
        public IEnumerable<Device> Devices { get; }

        /// <summary>
        /// Gets the scope result items, as a collection.
        /// </summary>
        [JsonProperty(PropertyName = "modules", Required = Required.AllowNull)]
        public IEnumerable<Module> Modules { get; }
    }
}
