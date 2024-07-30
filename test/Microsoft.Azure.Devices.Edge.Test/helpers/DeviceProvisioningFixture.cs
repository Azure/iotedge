// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public class DeviceProvisioningFixture : BaseFixture
    {
        protected static IEdgeDaemon daemon;

        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static async Task BeforeAllTestsAsync(TestContext msTestContext)
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(Context.Current.PackagePath, cts.Token);
            cli = daemon.GetCli();
        }
    }
}
