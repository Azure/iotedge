// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : Identity, IModuleIdentity
    {
        public ModuleIdentity(
            string iotHubHostname,
            string deviceId,
            string moduleId)
            : base(iotHubHostname, $"{deviceId}/{moduleId}")
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public override string ToString() => $"DeviceId: {this.DeviceId}; ModuleId: {this.ModuleId} [IotHubHostName: {this.IotHubHostname}]";
    }
}
