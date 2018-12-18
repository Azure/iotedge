// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinStoreEntity
    {
        public TwinStoreEntity()
            : this(new Twin(), Option.None<TwinCollection>())
        {
        }

        public TwinStoreEntity(Twin twin)
            : this(twin, Option.None<TwinCollection>())
        {
        }

        public TwinStoreEntity(Option<TwinCollection> reportedPropertiesPatch)
            : this(null, reportedPropertiesPatch)
        {
        }

        [JsonConstructor]
        public TwinStoreEntity(Twin twin, TwinCollection reportedPropertiesPatch)
            : this(twin, Option.Maybe(reportedPropertiesPatch))
        {
        }

        public TwinStoreEntity(Twin twin, Option<TwinCollection> reportedPropertiesPatch)
        {
            this.Twin = twin ?? new Twin();
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        public Twin Twin { get; }

        [JsonConverter(typeof(OptionConverter<TwinCollection>))]
        public Option<TwinCollection> ReportedPropertiesPatch { get; }
    }
}
