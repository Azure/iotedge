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

        protected bool Equals(ModuleIdentity other) =>
            base.Equals(other) &&
            string.Equals(this.DeviceId, other.DeviceId) &&
            string.Equals(this.ModuleId, other.ModuleId);

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

            return this.Equals((ModuleIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DeviceId.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ModuleId.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() =>
            $"DeviceId: {this.DeviceId}; " +
            $"ModuleId: {this.ModuleId} " +
            $"[IotHubHostName: {this.IotHubHostname}]";
    }
}
