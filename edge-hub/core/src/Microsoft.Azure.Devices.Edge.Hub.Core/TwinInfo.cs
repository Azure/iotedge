// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class TwinInfo
    {
        [JsonConstructor]
        public TwinInfo(TwinProperties twin, PropertyCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        public TwinProperties Twin { get; }

        public PropertyCollection ReportedPropertiesPatch { get; }
    }
}
