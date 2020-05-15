// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Serilog;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;

        public DeviceProvisioningFixture()
        {
            Log.Verbose($"DRB - First registry: {Context.Current.Registries.First().address}");
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath, Context.Current.Registries.First());
        }
    }
}
