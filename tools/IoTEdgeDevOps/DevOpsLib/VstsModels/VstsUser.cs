// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using Newtonsoft.Json;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/graph/users/list?view=azure-devops-rest-6.0
    [JsonConverter(typeof(JsonPathConverter))]
    public class VstsUser
    {
        [JsonProperty("mailAddress")]
        public string MailAddress { get; set; }

        [JsonProperty("displayName")]
        public string Name { get; set; }
    }
}

