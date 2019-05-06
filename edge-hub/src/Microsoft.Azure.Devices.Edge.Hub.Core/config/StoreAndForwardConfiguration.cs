// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class StoreAndForwardConfiguration : IEquatable<StoreAndForwardConfiguration>
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

        public bool Equals(StoreAndForwardConfiguration other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.TimeToLiveSecs == other.TimeToLiveSecs;
        }

        public override bool Equals(object obj)
            => this.Equals(obj as StoreAndForwardConfiguration);

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.TimeToLiveSecs * 397) ^ this.TimeToLive.GetHashCode();
            }
        }

        public static bool operator ==(StoreAndForwardConfiguration left, StoreAndForwardConfiguration right) => Equals(left, right);

        public static bool operator !=(StoreAndForwardConfiguration left, StoreAndForwardConfiguration right) => !Equals(left, right);
    }
}
