// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesModuleIdentity
    {
        public KubernetesModuleIdentity(string iotHubHostname, string gatewayHostname, string deviceId, string moduleId, IdentityProviderServiceCredentials credentials)
        {
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.GatewayHostname = gatewayHostname;
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.Credentials = Preconditions.CheckNotNull(credentials, nameof(credentials));
        }

        public string IotHubHostname { get; }

        public string GatewayHostname { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public IdentityProviderServiceCredentials Credentials { get; }
    }
}
