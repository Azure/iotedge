// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    // Refer to https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1 for schema
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsBuild
    {
        [JsonProperty("buildNumber")]
        public string BuildNumber { get; set; }

        [JsonProperty("_links.web.href")]
        public Uri WebUri { get; set; }

        [JsonProperty("status")]
        public VstsBuildStatus Status { get; set; }

        [JsonProperty("result")]
        public VstsBuildResult Result { get; set; }

        [JsonProperty("queueTime")]
        public DateTime QueueTime { get; set; }

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("finishTime")]
        public DateTime FinishTime { get; set; }
    }
}
