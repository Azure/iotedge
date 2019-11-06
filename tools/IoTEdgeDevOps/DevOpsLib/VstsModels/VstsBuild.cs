// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using Newtonsoft.Json;

    // Refer to https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1 for schema
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsBuild
    {
        [JsonProperty("definition.id")]
        public string DefinitionId { get; set; }

        [JsonProperty("buildNumber")]
        public string BuildNumber { get; set; }

        [JsonProperty("sourceBranch")]
        public string SourceBranch { get; set; }

        [JsonProperty("_links.sourceVersionDisplayUri.href")]
        public Uri SourceVersionDisplayUri { get; set; }

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

        public static VstsBuild GetBuildWithNoResult(int definitionId, string sourceBranch) =>
            new VstsBuild
            {
                DefinitionId = definitionId.ToString(),
                BuildNumber = string.Empty,
                SourceBranch = sourceBranch,
                SourceVersionDisplayUri = new Uri("https://dev.azure.com/msazure/One/_build"),
                WebUri = new Uri("https://dev.azure.com/msazure/One/_build"),
                Status = VstsBuildStatus.None,
                Result = VstsBuildResult.None,
                QueueTime = DateTime.MinValue,
                StartTime = DateTime.MinValue,
                FinishTime = DateTime.MinValue
            };
    }
}
