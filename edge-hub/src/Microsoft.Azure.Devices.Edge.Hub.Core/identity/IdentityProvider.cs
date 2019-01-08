// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityProvider : IIdentityProvider
    {
        readonly string iothubHostName;

        public IdentityProvider(string iothubHostName)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
        }

        public IIdentity Create(string id)
        {
            // If it is a module id, it will have the format "deviceId/moduleId"
            string[] parts = Preconditions.CheckNotNull(id, nameof(id)).Split('/');
            IIdentity identity = parts.Length == 2
                ? new ModuleIdentity(this.iothubHostName, parts[0], parts[1]) as IIdentity
                : new DeviceIdentity(this.iothubHostName, id);
            return identity;
        }

        public IIdentity Create(string deviceId, string moduleId)
        {
            IIdentity identity = string.IsNullOrWhiteSpace(moduleId)
                ? new DeviceIdentity(this.iothubHostName, deviceId)
                : new ModuleIdentity(this.iothubHostName, deviceId, moduleId) as IIdentity;
            return identity;
        }
    }
}
