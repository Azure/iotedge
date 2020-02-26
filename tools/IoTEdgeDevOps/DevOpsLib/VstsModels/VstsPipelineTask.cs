// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using Newtonsoft.Json;

    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsPipelineTask
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("finishTime")]
        public DateTime FinishTime { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("logUrl")]
        public Uri LogUrl { get; set; }
    }
}
