// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public class HubDeviceIdentity : IDeviceIdentity, IHubDeviceIdentity
    {
        string asString;

        public HubDeviceIdentity(
            string iotHubHostName,
            string id,
            bool isAuthenticated,
            string connectionString,
            AuthenticationScope scope,
            string policyName,
            string secret)
        {
            this.IotHubHostName = iotHubHostName;
            this.Id = id;
            this.IsAuthenticated = isAuthenticated;
            this.ConnectionString = connectionString;
            this.Scope = scope;
            this.PolicyName = policyName;
            this.Secret = secret;
        }

        public string IotHubHostName { get; }
        public bool IsAuthenticated { get; }

        public string ConnectionString { get; }

        public string Id { get; }

        public AuthenticationScope Scope { get; }

        public string PolicyName { get; }

        public string Secret { get; }
        
        public override string ToString()
        {
            if (this.asString == null)
            {
                string policy = string.IsNullOrEmpty(this.PolicyName) ? "<none>" : this.PolicyName;
                this.asString = $"{this.Id} [IotHubHostName: {this.IotHubHostName}; PolicyName: {policy}; Scope: {this.Scope}]";
            }
            return this.asString;
        }
    }
}