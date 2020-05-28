// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;

        public DeviceProvisioningFixture()
        {
            Option<Registry> bootstrapRegistry =
                Option.Maybe(Context.Current.Registries.First());
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath, Context.Current.EdgeAgentBootstrapImage, bootstrapRegistry);
        }
    }
}
