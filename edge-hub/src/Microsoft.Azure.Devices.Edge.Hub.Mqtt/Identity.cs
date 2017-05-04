// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public abstract class Identity : IDeviceIdentity, IIdentity
    {
        protected Identity(
            string iotHubHostName,
            bool isAuthenticated,
            string connectionString,
            AuthenticationScope scope,
            string policyName,
            string secret)
        {
            this.IotHubHostName = iotHubHostName;
            this.IsAuthenticated = isAuthenticated;
            this.ConnectionString = connectionString;
            this.Scope = scope;
            this.PolicyName = policyName;
            this.Secret = secret;
        }

        public string IotHubHostName { get; }

        public bool IsAuthenticated { get; }

        public string ConnectionString { get; }

        public abstract string Id { get; }

        public AuthenticationScope Scope { get; }

        public string PolicyName { get; }

        public string Secret { get; }
    }
}