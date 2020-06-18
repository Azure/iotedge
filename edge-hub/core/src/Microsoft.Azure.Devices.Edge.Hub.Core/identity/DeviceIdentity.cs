// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceIdentity : Identity, IDeviceIdentity
    {
        public DeviceIdentity(
            string iotHubHostName,
            string deviceId)
            : base(iotHubHostName, deviceId)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public string DeviceId { get; }

        public override string ToString() => $"DeviceId: {this.DeviceId} [IotHubHostName: {this.IotHubHostname}]";
    }
}
