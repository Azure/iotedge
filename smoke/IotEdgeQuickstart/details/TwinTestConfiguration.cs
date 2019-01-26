// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;

namespace IotEdgeQuickstart.details
{
    using Newtonsoft.Json;

    public class TwinTestConfiguration
    {
        [JsonProperty(PropertyName = "moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty(PropertyName = "twinTest")]
        public Dictionary<string, Dictionary<string, string>> TwinTest; 
    }
}
