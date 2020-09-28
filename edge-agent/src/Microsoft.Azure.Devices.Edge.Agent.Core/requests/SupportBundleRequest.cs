// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SupportBundleRequest
    {
        [JsonConstructor]
        public SupportBundleRequest(string schemaVersion, string sasUrl, string since, string until, bool? edgeRuntimeOnly)
            : this(schemaVersion, sasUrl, since, until, edgeRuntimeOnly, new NestedEdgeParentUriParser())
        {
        }

        public SupportBundleRequest(string schemaVersion, string sasUrl, string since, string until, bool? edgeRuntimeOnly, INestedEdgeParentUriParser parser)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.SasUrl = Preconditions.CheckNotNull(sasUrl, nameof(sasUrl));
            this.Since = Option.Maybe(since);
            this.Until = Option.Maybe(until);
            this.EdgeRuntimeOnly = Option.Maybe(edgeRuntimeOnly);

            Option<string> url = parser.ParseURI(this.SasUrl);
            this.SasUrl = url.GetOrElse(this.SasUrl);
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
