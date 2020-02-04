// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;

        public DeviceProvisioningFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
        }
    }
}
