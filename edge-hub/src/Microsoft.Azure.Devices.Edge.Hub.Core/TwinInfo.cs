// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinInfo
    {
        public Twin Twin { get; }

        public TwinCollection ReportedPropertiesPatch { get; }

        [JsonConstructor]
        public TwinInfo(Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }
    }
}
