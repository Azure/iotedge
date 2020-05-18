// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;

        public DeviceProvisioningFixture()
        {
            (string serverAddress, string username, string password) firstRegistry = Context.Current.Registries.First();
            (string image, string serverAddress, string username, string password) bootstrapAgentInfo =
                (Context.Current.EdgeAgentBootstrapImage, firstRegistry.serverAddress, firstRegistry.username, firstRegistry.password);
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath, bootstrapAgentInfo);
        }
    }
}
