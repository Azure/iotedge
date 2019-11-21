// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using Newtonsoft.Json;

    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsReleaseEnvironment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("definitionEnvironmentId")] 
        public int DefinitionId { get; set; }

        [JsonProperty("status")]
        public VstsEnvironmentStatus Status { get; set; }
    }
}
