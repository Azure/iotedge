// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MqttMessage : IMessage
    {
        public byte[] Body { get; }

        public IDictionary<string, string> Properties { get; }

        public IDictionary<string, string> SystemProperties { get; }

        MqttMessage(byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
        {
            this.Body = body;
            this.Properties = properties;
            this.SystemProperties = systemProperties;
        }

        public bool Equals(MqttMessage other)
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
                this.Properties.Keys.Count() == other.Properties.Keys.Count() &&
                this.Properties.Keys.All(
                    key => other.Properties.ContainsKey(key) && Equals(this.Properties[key], other.Properties[key]) &&
                        this.SystemProperties.Keys.Count() == other.SystemProperties.Keys.Count() &&
                        this.SystemProperties.Keys.All(skey => other.SystemProperties.ContainsKey(skey) && Equals(this.SystemProperties[skey], other.SystemProperties[skey])));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == this.GetType() && this.Equals((MqttMessage)obj);
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

            public Builder SetProperties(IDictionary<string, string> properties)
            {
                this.properties = new Dictionary<string, string>(Preconditions.CheckNotNull(properties), StringComparer.OrdinalIgnoreCase);
                return this;
            }

            public Builder SetSystemProperties(IDictionary<string, string> systemProperties)
            {
                this.systemProperties = new Dictionary<string, string>(Preconditions.CheckNotNull(systemProperties), StringComparer.OrdinalIgnoreCase);
                return this;
            }

            public MqttMessage Build()
            {
                if (this.properties == null)
                    this.properties = new Dictionary<string, string>();
                if (this.systemProperties == null)
                    this.systemProperties = new Dictionary<string, string>();

                return new MqttMessage(this.body, this.properties, this.systemProperties);
            }

        }
    }
}