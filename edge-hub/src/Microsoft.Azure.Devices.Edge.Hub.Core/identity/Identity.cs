// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public abstract class Identity : IIdentity
    {
        protected Identity(
            string iotHubHostName,
            string connectionString,
            AuthenticationScope scope,
            string policyName,
            string secret)
        {
            this.IotHubHostName = iotHubHostName;
            this.ConnectionString = connectionString;
            this.Scope = scope;
            this.PolicyName = policyName;
            this.Secret = secret;
        }

        public string IotHubHostName { get; }

        public string ConnectionString { get; }

        public abstract string Id { get; }

        public AuthenticationScope Scope { get; }

        public string PolicyName { get; }

        public string Secret { get; }
    }
}