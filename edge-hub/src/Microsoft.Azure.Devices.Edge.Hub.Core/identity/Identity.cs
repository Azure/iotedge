// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System.Collections.Generic;

    public abstract class Identity : IIdentity
    {
        protected Identity(string iotHubHostName)
        {
            this.IotHubHostName = iotHubHostName;
        }

        public string IotHubHostName { get; }

        public abstract string Id { get; }

        public override bool Equals(object obj)
        {
            return obj is Identity identity &&
                this.IotHubHostName == identity.IotHubHostName &&
                this.Id == identity.Id;
        }

        public override int GetHashCode()
        {
            int hashCode = -1379229077;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.IotHubHostName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Id);
            return hashCode;
        }
    }
}
