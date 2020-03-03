// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/deployments/list?view=azure-devops-rest-5.1#deployment 
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsReleaseDeployment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("attempt")]
        public int Attempt { get; set; }

        [JsonProperty("deploymentStatus")]
        public VstsDeploymentStatus Status { get; set; }

        [JsonProperty("lastModifiedOn")]
        public DateTime LastModifiedOn { get; set; }

        [JsonProperty("releaseDeployPhases[0].deploymentJobs[0].tasks")]
        public List<VstsPipelineTask> Tasks { get; set; }
    }
}
