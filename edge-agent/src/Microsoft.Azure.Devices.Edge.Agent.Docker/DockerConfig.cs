// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    // TODO add PortBindings to equality check
    public class DockerConfig : IEquatable<DockerConfig>
    {
        [JsonProperty(Required = Required.Always)]
        public string Image { get; }

        [JsonIgnore]
        public string Tag { get; }

        [JsonIgnore]
        public IList<PortBinding> PortBindings { get; }

        [JsonConstructor]
        public DockerConfig(string image)
            : this(image, "latest", ImmutableList<PortBinding>.Empty)
        {
        }

        public DockerConfig(string image, string tag)
            : this(image, tag, ImmutableList<PortBinding>.Empty)
        {
        }

        public DockerConfig(string image, string tag, IList<PortBinding> portBindings)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
            this.Tag = Preconditions.CheckNotNull(tag, nameof(tag));
            this.PortBindings = Preconditions.CheckNotNull(portBindings, nameof(portBindings)).ToImmutableList();
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerConfig);

        public bool Equals(DockerConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(this.Image, other.Image) &&
                string.Equals(this.Tag, other.Tag);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Image?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.Tag?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}