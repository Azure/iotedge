// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class Identity : IIdentity
    {
        protected Identity(string iotHubHostname, string id)
        {
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
        }

        public string IotHubHostname { get; }

        public string Id { get; }

        protected bool Equals(Identity other) =>
            string.Equals(this.IotHubHostname, other.IotHubHostname) &&
            string.Equals(this.Id, other.Id);

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

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((Identity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.IotHubHostname.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Id.GetHashCode();
                return hashCode;
            }
        }
    }
}
