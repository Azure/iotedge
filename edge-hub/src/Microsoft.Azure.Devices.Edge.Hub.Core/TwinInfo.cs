// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinInfo
    {
        [JsonConstructor]
        public TwinInfo(Shared.Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        public Twin Twin { get; }

        public TwinCollection ReportedPropertiesPatch { get; }
    }    
}
