// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public class CustomCertificatesFixture : SasManualProvisioningFixture
    {
        // Do nothing; everything happens at [OneTimeSetUp] instead. We do this to avoid
        // creating a new device for every permutation of the Transparent Gateway tests.
        protected override Task BeforeTestTimerStarts() => Task.CompletedTask;

        [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
        public static async Task SetUpCustomCertificatesAsync(TestContext msTestContext)
        {
            await Profiler.Run(
                () => SasProvisionEdgeAsync(true),
                "Completed edge manual provisioning with SAS token");
        }
    }
}
