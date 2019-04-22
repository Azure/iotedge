// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinTestConfiguration
    {
        [JsonProperty(PropertyName = "moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty(PropertyName = "properties")]
        public TwinProperties Properties { get; set; }
    }
}
