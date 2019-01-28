// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TwinTestConfiguration
    {
        [JsonProperty(PropertyName = "moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty(PropertyName = "twinTest")]
        public Dictionary<string, Dictionary<string, string>> TwinTest;
    }
}
