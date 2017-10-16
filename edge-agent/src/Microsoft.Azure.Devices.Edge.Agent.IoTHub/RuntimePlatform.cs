// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class RuntimePlatform : IEquatable<RuntimePlatform>
    {
        [JsonConstructor]
        public RuntimePlatform(string operatingSystem = null, string architecture = null)
        {
            this.OperatingSystem = operatingSystem ?? string.Empty;
            this.Architecture = architecture ?? string.Empty;
        }

        [JsonProperty(PropertyName = "os")]
        public string OperatingSystem { get; }

        [JsonProperty(PropertyName = "architecture")]
        public string Architecture { get; }

        public RuntimePlatform Clone() => new RuntimePlatform(this.OperatingSystem, this.Architecture);

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimePlatform);
        }

        public bool Equals(RuntimePlatform other)
        {
            return other != null &&
                   this.OperatingSystem == other.OperatingSystem &&
                   this.Architecture == other.Architecture;
        }

        public override int GetHashCode()
        {
            var hashCode = 577840947;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(OperatingSystem);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Architecture);
            return hashCode;
        }

        public static bool operator ==(RuntimePlatform platform1, RuntimePlatform platform2)
        {
            return EqualityComparer<RuntimePlatform>.Default.Equals(platform1, platform2);
        }

        public static bool operator !=(RuntimePlatform platform1, RuntimePlatform platform2)
        {
            return !(platform1 == platform2);
        }
    }
}
