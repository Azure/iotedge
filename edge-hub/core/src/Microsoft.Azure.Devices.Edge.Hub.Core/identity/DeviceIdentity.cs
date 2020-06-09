// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceIdentity : Identity, IDeviceIdentity
    {
        readonly Lazy<string> asString;

        public DeviceIdentity(
            string iotHubHostName,
            string deviceId)
            : base(iotHubHostName)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.asString = new Lazy<string>(() => $"DeviceId: {this.DeviceId} [IotHubHostName: {this.IotHubHostName}]");
        }

        public string DeviceId { get; }

        public override string Id => this.DeviceId;

        public override string ToString() => this.asString.Value;
    }
}
