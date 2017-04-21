// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DockerConfig : IEquatable<DockerConfig>
    {
        public string Image { get; }

        public DockerConfig(string image)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
        }

        public bool Equals(DockerConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            return ReferenceEquals(this, other) || string.Equals(this.Image, other.Image);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is DockerConfig && this.Equals(obj);
        }

        public override int GetHashCode() => this.Image?.GetHashCode() ?? 0;
    }
}