// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/releases/get%20release%20environment?view=azure-devops-rest-5.1#releaseenvironment
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
