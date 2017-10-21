// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ConfigurationInfo : IEquatable<ConfigurationInfo>
    {
        [JsonConstructor]
        public ConfigurationInfo(string id = "")
        {
            this.Id = id ?? string.Empty;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; }

        public override bool Equals(object obj) => this.Equals(obj as ConfigurationInfo);

        public bool Equals(ConfigurationInfo other) => other != null && this.Id == other.Id;

        public override int GetHashCode()
        {
            return 2108858624 + EqualityComparer<string>.Default.GetHashCode(this.Id);
        }
    }
}
