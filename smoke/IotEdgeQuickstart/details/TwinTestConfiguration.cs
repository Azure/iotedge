// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class TwinTestProperties
    {
        [JsonProperty(PropertyName = "desired")]
        public PropertyCollection Desired { get; set; }

        [JsonProperty(PropertyName = "reported")]
        public PropertyCollection Reported { get; set; }
    }

    public class TwinTestConfiguration
    {
        [JsonProperty(PropertyName = "moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty(PropertyName = "properties")]
        public TwinTestProperties Properties { get; set; }
    }
}
