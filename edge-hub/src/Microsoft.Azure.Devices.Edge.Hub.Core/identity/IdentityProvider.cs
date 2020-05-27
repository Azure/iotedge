// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityProvider : IIdentityProvider
    {
        readonly string iotHubHostname;
        readonly Option<string> gatewayHostname;

        public IdentityProvider(string iothubHostname, Option<string> gatewayHostname)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
            this.gatewayHostname = gatewayHostname;
        }

        public IIdentity Create(string id)
        {
            // If it is a module id, it will have the format "deviceId/moduleId"
            string[] parts = Preconditions.CheckNotNull(id, nameof(id)).Split('/');
            IIdentity identity = parts.Length == 2
                ? new ModuleIdentity(this.iotHubHostname, this.gatewayHostname, parts[0], parts[1]) as IIdentity
                : new DeviceIdentity(this.iotHubHostname, this.gatewayHostname, id);
            return identity;
        }

        public IIdentity Create(string deviceId, string moduleId)
        {
            IIdentity identity = string.IsNullOrWhiteSpace(moduleId)
                ? new DeviceIdentity(this.iotHubHostname, this.gatewayHostname, deviceId)
                : new ModuleIdentity(this.iotHubHostname, this.gatewayHostname, deviceId, moduleId) as IIdentity;
            return identity;
        }
    }
}
