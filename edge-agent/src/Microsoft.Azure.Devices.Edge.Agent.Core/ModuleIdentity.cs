// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : IModuleIdentity
    {
        public ModuleIdentity(string iotHubHostname, string gatewayHostname, string deviceId, string moduleId, ICredentials credentials)
        {
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(this.IotHubHostname));
            this.GatewayHostname = gatewayHostname;
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(this.DeviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(this.ModuleId));
            this.Credentials = Preconditions.CheckNotNull(credentials, nameof(this.Credentials));
        }

        public string IotHubHostname { get; }

        public string GatewayHostname { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public ICredentials Credentials { get; }
    }
}
