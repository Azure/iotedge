// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        public SasManualProvisioningFixture()
            : base(string.Empty)
        {
        }

        [OneTimeSetUp]
        public async Task SasProvisionEdgeAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;
                        DateTime startTime = DateTime.Now;

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            AuthenticationType.Sas,
                            null,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        await this.ManuallyProvisionEdgeSasAsync(device, startTime, token);
                    }
                },
                "Completed edge manual provisioning with SAS token");
        }
    }
}
