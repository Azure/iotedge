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
            : this(Option.None<Twin>(), Option.None<TwinCollection>())
        {
        }

        public TwinStoreEntity(Twin twin)
            : this(Option.Maybe(twin), Option.None<TwinCollection>())
        {
        }

        public TwinStoreEntity(TwinCollection reportedPropertiesPatch)
            : this(Option.None<Twin>(), Option.Maybe(reportedPropertiesPatch))
        {
        }

        //[JsonConstructor]
        //public TwinStoreEntity(string twin, string reportedPropertiesPatch)
        //{
        //    this.Twin = string.IsNullOrWhiteSpace(twin)
        //        ? Option.None<Twin>()
        //        : Option.Some(JsonConvert.DeserializeObject<Twin>(twin));

        //    this.ReportedPropertiesPatch = string.IsNullOrWhiteSpace(reportedPropertiesPatch)
        //        ? Option.None<TwinCollection>()
        //        : Option.Some(JsonConvert.DeserializeObject<TwinCollection>(reportedPropertiesPatch));
        //}

        [JsonConstructor]
        public TwinStoreEntity(Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = Option.Maybe(twin);
            this.ReportedPropertiesPatch = reportedPropertiesPatch?.Count != 0
                ? Option.Some(reportedPropertiesPatch)
                : Option.None<TwinCollection>();
        }

        public TwinStoreEntity(Option<Twin> twin, Option<TwinCollection> reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        [JsonConverter(typeof(OptionConverter<Twin>))]
        public Option<Twin> Twin { get; }

        [JsonConverter(typeof(OptionConverter<TwinCollection>))]
        public Option<TwinCollection> ReportedPropertiesPatch { get; }
    }
}
