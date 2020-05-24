// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentity : IModuleIdentity
    {
        public ModuleIdentity(
            string iotHubHostname,
            string edgeDeviceHostname,
            Option<string> parentEdgeHostname,
            string deviceId,
            string moduleId,
            ICredentials credentials)
        {
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.ParentEdgeHostname = parentEdgeHostname;
            this.EdgeDeviceHostname = Preconditions.CheckNonWhiteSpace(edgeDeviceHostname, nameof(edgeDeviceHostname));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.Credentials = Preconditions.CheckNotNull(credentials, nameof(this.Credentials));
        }

        public string IotHubHostname { get; }

        public string EdgeDeviceHostname { get; }

        public Option<string> ParentEdgeHostname { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public ICredentials Credentials { get; }
    }
}
