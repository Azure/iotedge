// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.IoTHub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Message : IMessage
    {
        public byte[] Body { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public IReadOnlyDictionary<string, string> SystemProperties { get; }

        public Message(byte[] body)
            : this(body, new Dictionary<string, string>(), new Dictionary<string, string>())
        {
        }

        public Message(byte[] body, IDictionary<string, string> properties)
            : this(body, properties, new Dictionary<string, string>())
        {
        }

        public Message(byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
        {
            this.Body = Preconditions.CheckNotNull(body);
            this.Properties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Preconditions.CheckNotNull(properties), StringComparer.OrdinalIgnoreCase));
            this.SystemProperties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Preconditions.CheckNotNull(systemProperties), StringComparer.OrdinalIgnoreCase));
        }

        public bool Equals(Message other)
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
                this.Properties.Keys.All(key => other.Properties.ContainsKey(key) && Equals(this.Properties[key], other.Properties[key]) &&
                this.SystemProperties.Keys.Count() == other.SystemProperties.Keys.Count() &&
                this.SystemProperties.Keys.All(skey => other.SystemProperties.ContainsKey(skey) && Equals(this.SystemProperties[skey], other.SystemProperties[skey])));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == this.GetType() && this.Equals((Message)obj);
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
    }
}