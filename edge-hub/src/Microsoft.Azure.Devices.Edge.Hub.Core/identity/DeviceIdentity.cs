// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceIdentity : Identity, IDeviceIdentity
    {
        public DeviceIdentity(
            string iotHubHostName,
            Option<string> gatewayHostname,
            string deviceId)
            : base(iotHubHostName, gatewayHostname, deviceId)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public string DeviceId { get; }

        protected bool Equals(DeviceIdentity other) =>
            base.Equals(other) &&
            string.Equals(this.DeviceId, other.DeviceId);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((DeviceIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DeviceId.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() =>
            $"DeviceId: {this.DeviceId} " +
            $"[IotHubHostName: {this.IotHubHostname}]" +
            $"{this.GatewayHostname.Map(v => $" [GatewayHostname: {v}]")}";
    }
}
