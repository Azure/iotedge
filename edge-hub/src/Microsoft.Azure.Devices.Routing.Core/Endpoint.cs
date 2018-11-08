// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public abstract class Endpoint : IEquatable<Endpoint>
    {
        protected Endpoint(string id)
            : this(id, id, string.Empty)
        {
        }

        protected Endpoint(string id, string name, string iotHubName)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.Name = Preconditions.CheckNotNull(name);
            this.IotHubName = Preconditions.CheckNotNull(iotHubName);
        }

        /// <summary>
        /// Gets endpoint identifier. This must be globally unique.
        /// </summary>
        public virtual string Id { get; }

        public string IotHubName { get; }

        public string Name { get; }

        public abstract string Type { get; }

        public abstract IProcessor CreateProcessor();

        public bool Equals(Endpoint other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            // Name is intentionally left out of equality because it can be updated on the
            // endpoint without changing the endpoint's functionality
            return ReferenceEquals(this, other) || string.Equals(this.Id, other.Id);
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

            return obj.GetType() == this.GetType() && this.Equals((Endpoint)obj);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public abstract void LogUserMetrics(long messageCount, long latencyInMs);
    }
}
