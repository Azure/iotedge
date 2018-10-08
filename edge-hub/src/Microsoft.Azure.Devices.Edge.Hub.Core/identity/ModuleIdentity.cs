// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : Identity, IModuleIdentity
    {
        readonly Lazy<string> asString;

        public ModuleIdentity(
            string iotHubHostName,
            string deviceId,
            string moduleId)
            : base(iotHubHostName)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.asString = new Lazy<string>(() => $"DeviceId: {this.DeviceId}; ModuleId: {this.ModuleId} [IotHubHostName: {this.IotHubHostName}]");
        }

        public string DeviceId { get; }

        public override string Id => FormattableString.Invariant($"{this.DeviceId}/{this.ModuleId}");

        public string ModuleId { get; }

        public override string ToString() => this.asString.Value;
    }
}
