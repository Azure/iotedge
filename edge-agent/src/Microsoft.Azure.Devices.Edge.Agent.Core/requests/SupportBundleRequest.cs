// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SupportBundleRequest
    {
        public SupportBundleRequest(string schemaVersion, string sasUrl, string since, string until, bool? edgeRuntimeOnly)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.SasUrl = Preconditions.CheckNotNull(sasUrl, nameof(sasUrl));
            this.Since = Option.Maybe(since);
            this.Until = Option.Maybe(until);
            this.EdgeRuntimeOnly = Option.Maybe(edgeRuntimeOnly);
        }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; }

        [JsonIgnore]
        public string SasUrl { get; }

        [JsonProperty("since")]
        public Option<string> Since { get; }

        [JsonProperty("until")]
        public Option<string> Until { get; }

        [JsonProperty("edgeRuntimeOnly")]
        public Option<bool> EdgeRuntimeOnly { get; }
    }
}
