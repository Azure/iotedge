// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ResourceName
    {
        public readonly string DeviceId;
        public readonly string Hostname;

        public ResourceName(string hostname, string deviceId)
        {
            this.Hostname = Preconditions.CheckNonWhiteSpace(hostname, nameof(hostname));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public static implicit operator string(ResourceName name) => name.ToString();

        public bool Equals(string other) => this.ToString().Equals(other, StringComparison.OrdinalIgnoreCase);

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

            return this.Equals(obj.ToString());
        }

        public override int GetHashCode() => this.ToString().GetHashCode();

        public override string ToString() => KubeUtils.SanitizeK8sValue(this.Hostname) + Constants.K8sNameDivider + KubeUtils.SanitizeK8sValue(this.DeviceId);
    }
}
