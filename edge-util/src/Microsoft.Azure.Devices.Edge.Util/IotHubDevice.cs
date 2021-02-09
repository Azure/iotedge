// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class IotHubDevice : Device
    {
        public IotHubDevice()
        {
        }

        public IotHubDevice(string id)
            : base(id)
        {
        }

        [JsonProperty(PropertyName = "parentScopes", NullValueHandling = NullValueHandling.Ignore)]
        public virtual IEnumerable<string> ParentScopes { get; set; }
    }
}
