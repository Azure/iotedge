// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinInfo
    {
        [JsonConstructor]
        public TwinInfo(Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        public Twin Twin { get; }

        public TwinCollection ReportedPropertiesPatch { get; }
    }

    public class TwinInfo2
    {
        public Twin Twin { get; }

        [JsonConverter(typeof(OptionConverter<TwinCollection>))]
        public Option<TwinCollection> ReportedPropertiesPatch { get; }

        public TwinInfo2()
            : this(new Twin(), Option.None<TwinCollection>())
        {
        }

        public TwinInfo2(Twin twin)
            : this(twin, Option.None<TwinCollection>())
        {
        }

        public TwinInfo2(Option<TwinCollection> reportedPropertiesPatch)
            : this(null, reportedPropertiesPatch)
        {
        }

        [JsonConstructor]
        public TwinInfo2(Twin twin, TwinCollection reportedPropertiesPatch)
            : this(twin, Option.Maybe(reportedPropertiesPatch))
        {
        }

        public TwinInfo2(Twin twin, Option<TwinCollection> reportedPropertiesPatch)            
        {
            this.Twin = twin ?? new Twin();
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }
    }
}
