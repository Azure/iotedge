// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class EnvVal : IEquatable<EnvVal>
    {
        [JsonConstructor]
        public EnvVal(string value)
        {
            this.Value = value ?? string.Empty;
        }

        [JsonProperty("value")]
        public string Value { get; }

        public override bool Equals(object obj) => this.Equals(obj as EnvVal);

        public bool Equals(EnvVal other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.Value, other.Value);
        }

        public override int GetHashCode()
        {
            return -1937169414 + EqualityComparer<string>.Default.GetHashCode(this.Value);
        }
    }
}
