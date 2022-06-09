// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected IEdgeDaemon daemon;

        [OneTimeSetUp]
        protected async Task BeforeAllTestsAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            Option<Registry> bootstrapRegistry = Option.Maybe(Context.Current.Registries.FirstOrDefault());
            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(
                Context.Current.InstallerPath,
                cts.Token);
        }
    }
}
