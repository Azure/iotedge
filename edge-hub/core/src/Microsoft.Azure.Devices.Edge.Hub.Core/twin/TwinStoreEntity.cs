// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class TwinStoreEntity
    {
        public TwinStoreEntity()
            : this(Option.None<TwinProperties>(), Option.None<PropertyCollection>())
        {
        }

        public TwinStoreEntity(TwinProperties twin)
            : this(Option.Maybe(twin), Option.None<PropertyCollection>())
        {
        }

        public TwinStoreEntity(PropertyCollection reportedPropertiesPatch)
            : this(Option.None<TwinProperties>(), Option.Maybe(reportedPropertiesPatch))
        {
        }

        [JsonConstructor]
        public TwinStoreEntity(TwinProperties twin, PropertyCollection reportedPropertiesPatch)
        {
            this.Twin = Option.Maybe(twin);
            this.ReportedPropertiesPatch = reportedPropertiesPatch != null && reportedPropertiesPatch.Count != 0
                ? Option.Some(reportedPropertiesPatch)
                : Option.None<PropertyCollection>();
        }

        public TwinStoreEntity(Option<TwinProperties> twin, Option<PropertyCollection> reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        [JsonConverter(typeof(OptionConverter<TwinProperties>))]
        public Option<TwinProperties> Twin { get; }

        [JsonConverter(typeof(OptionConverter<PropertyCollection>))]
        public Option<PropertyCollection> ReportedPropertiesPatch { get; }
    }
}
