// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeMessage : IMessage
    {
        public byte[] Body { get; }

        public IDictionary<string, string> Properties { get; }

        public IDictionary<string, string> SystemProperties { get; }

        public EdgeMessage(byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
        {
            this.Body = Preconditions.CheckNotNull(body, nameof(body));
            this.Properties = Preconditions.CheckNotNull(properties, nameof(properties));
            this.SystemProperties = Preconditions.CheckNotNull(systemProperties, nameof(systemProperties));
        }

        public bool Equals(EdgeMessage other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Body.SequenceEqual(other.Body) &&
                this.Properties.Keys.Count == other.Properties.Keys.Count &&
                this.Properties.Keys.All(key => other.Properties.ContainsKey(key) && Equals(this.Properties[key], other.Properties[key])) &&
                this.SystemProperties.Keys.Count == other.SystemProperties.Keys.Count &&
                this.SystemProperties.Keys.All(skey => other.SystemProperties.ContainsKey(skey) && Equals(this.SystemProperties[skey], other.SystemProperties[skey]));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == this.GetType() && this.Equals((EdgeMessage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Body.Aggregate(hash, (acc, b) => acc * 31 + b);
                hash = this.Properties.Aggregate(hash, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value.GetHashCode());
                hash = this.SystemProperties.Aggregate(hash, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value.GetHashCode());

                return hash;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public class Builder
        {
            readonly byte[] body;
            IDictionary<string, string> properties;
            IDictionary<string, string> systemProperties;

            public Builder(byte[] body)
            {
                this.body = Preconditions.CheckNotNull(body);
            }

            public Builder SetProperties(IDictionary<string, string> props)
            {
                this.properties = new Dictionary<string, string>(Preconditions.CheckNotNull(props), StringComparer.OrdinalIgnoreCase);
                return this;
            }

            public Builder SetSystemProperties(IDictionary<string, string> sysProps)
            {
                this.systemProperties = new Dictionary<string, string>(Preconditions.CheckNotNull(sysProps), StringComparer.OrdinalIgnoreCase);
                return this;
            }

            public EdgeMessage Build()
            {
                if (this.properties == null)
                    this.properties = new Dictionary<string, string>();
                if (this.systemProperties == null)
                    this.systemProperties = new Dictionary<string, string>();

                return new EdgeMessage(this.body, this.properties, this.systemProperties);
            }

        }
    }
}
