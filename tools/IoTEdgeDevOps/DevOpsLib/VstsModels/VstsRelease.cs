// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/releases/list?view=azure-devops-rest-5.1#release
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsRelease
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("releaseDefinition.id")]
        public int DefinitionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("_links.web.href")]
        public Uri WebUri { get; set; }

        [JsonProperty("environments")]
        public List<VstsReleaseEnvironment> Environments { get; set; }
    }
}
