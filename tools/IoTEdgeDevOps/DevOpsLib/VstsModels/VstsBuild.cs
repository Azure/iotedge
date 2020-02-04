// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsBuild : IEquatable<VstsBuild>
    {
        [JsonProperty("definition.id")]
        public BuildDefinitionId DefinitionId { get; set; }

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

        public static VstsBuild CreateBuildWithNoResult(BuildDefinitionId buildDefinitionId, string sourceBranch) =>
            new VstsBuild
            {
                DefinitionId = buildDefinitionId,
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

        public bool Equals(VstsBuild other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.BuildNumber == other.BuildNumber;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((VstsBuild) obj);
        }

        public override int GetHashCode() => this.BuildNumber.GetHashCode();
    }
}
