// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using static System.FormattableString;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : Identity, IModuleIdentity
    {
        readonly Lazy<string> asString;

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

            this.asString = new Lazy<string>(
                () =>
                {
                    string policy = string.IsNullOrEmpty(this.PolicyName) ? "<none>" : this.PolicyName;
                    return $"DeviceId: {this.DeviceId}; ModuleId: {this.ModuleId} [IotHubHostName: {this.IotHubHostName}; PolicyName: {policy}; Scope: {this.Scope}]";
                });
        }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public override string Id => Invariant($"{this.DeviceId}/{this.ModuleId}");

        public override string ToString() => this.asString.Value;
    }
}