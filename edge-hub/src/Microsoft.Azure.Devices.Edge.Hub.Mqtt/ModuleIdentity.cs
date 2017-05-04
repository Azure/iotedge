// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using static System.FormattableString;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : Identity, IModuleIdentity
    {
        string asString;

        public ModuleIdentity(string iotHubHostName, 
            string deviceId, 
            string moduleId, 
            bool isAuthenticated, 
            string connectionString, 
            AuthenticationScope scope, 
            string policyName, 
            string secret)
            : base(iotHubHostName, isAuthenticated, connectionString, scope, policyName, secret)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public override string Id => Invariant($"{this.DeviceId}/{this.ModuleId}");

        public override string ToString()
        {
            if (this.asString == null)
            {
                string policy = string.IsNullOrEmpty(this.PolicyName) ? "<none>" : this.PolicyName;
                this.asString = $"DeviceId: {this.DeviceId}; ModuleId: {this.ModuleId} [IotHubHostName: {this.IotHubHostName}; PolicyName: {policy}; Scope: {this.Scope}]";
            }
            return this.asString;
        }
    }
}