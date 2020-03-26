// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;

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

                        string deviceId =
                            $"e2e-{string.Concat(Dns.GetHostName().Take(14)).TrimEnd(new[] { '-' })}-{DateTime.Now:yyMMdd'-'HHmmss'.'fff}";

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            AuthenticationType.Sas,
                            null,
                            token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.runtime = new EdgeRuntime(
                            device.Id,
                            Context.Current.EdgeAgentImage,
                            Context.Current.EdgeHubImage,
                            Context.Current.Proxy,
                            Context.Current.Registries,
                            Context.Current.OptimizeForPerformance,
                            this.iotHub);

                        await this.ConfigureAsync(
                            config =>
                            {
                                config.SetDeviceConnectionString(device.ConnectionString);
                                config.Update();
                                return Task.FromResult((
                                    "with connection string for device '{Identity}'",
                                    new object[] { device.Id }));
                            },
                            device,
                            startTime,
                            token
                        );
                    }
                },
                "Completed edge manual provisioning with SAS token");
        }
    }
}
