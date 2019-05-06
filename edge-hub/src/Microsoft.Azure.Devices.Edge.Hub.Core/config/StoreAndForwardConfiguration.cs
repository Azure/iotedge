// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class StoreAndForwardConfiguration
    {
        public StoreAndForwardConfiguration(int timeToLiveSecs)
            : this(timeToLiveSecs, null)
        {
        }

        [JsonConstructor]
        public StoreAndForwardConfiguration(int timeToLiveSecs, long? maxStorageSpaceBytes)
        {
            this.TimeToLiveSecs = timeToLiveSecs;
            this.TimeToLive = timeToLiveSecs < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(timeToLiveSecs);
            this.MaxStorageSpaceBytes = Option.Maybe(maxStorageSpaceBytes);
        }

        [JsonProperty(PropertyName = "timeToLiveSecs")]
        public int TimeToLiveSecs { get; }

        [JsonProperty(PropertyName = "maxStorageSpaceBytes")]
        [JsonConverter(typeof(OptionConverter<long>), true)]
        public Option<long> MaxStorageSpaceBytes { get; }

        [JsonIgnore]
        public TimeSpan TimeToLive { get; }
    }
}
