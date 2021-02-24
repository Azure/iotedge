// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;
    using Serilog;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;

        protected override Task BeforeTestTimerStarts() => this.SasProvisionEdgeAsync();

        protected virtual async Task SasProvisionEdgeAsync(bool withCerts = false)
        {
            using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
            {
                CancellationToken token = cts.Token;
                DateTime startTime = DateTime.Now;

                EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                    Context.Current.DeviceId.GetOrElse(DeviceId.Current.Generate()),
                    Context.Current.ParentDeviceId,
                    this.IotHub,
                    AuthenticationType.Sas,
                    null,
                    token);
                Log.Information($"Device ID {device.Id}");

                Context.Current.DeleteList.TryAdd(device.Id, device);

                this.runtime = new EdgeRuntime(
                    device.Id,
                    Context.Current.EdgeAgentImage,
                    Context.Current.EdgeHubImage,
                    Context.Current.EdgeProxy,
                    Context.Current.Registries,
                    Context.Current.OptimizeForPerformance,
                    this.IotHub);

                // This is a temporary solution see ticket: 9288683
                if (!Context.Current.ISA95Tag)
                {
                    TestCertificates testCerts;
                    (testCerts, this.ca) = await TestCertificates.GenerateCertsAsync(device.Id, token);

                    await this.ConfigureDaemonAsync(
                        config =>
                        {
                            testCerts.AddCertsToConfig(config);

                            config.SetManualSasProvisioning(Context.Current.ParentHostname.GetOrElse(device.HubHostname), device.Id, device.SharedAccessKey);

                            config.Update();
                            return Task.FromResult((
                                "with connection string for device '{Identity}'",
                                new object[] { device.Id }));
                        },
                        device,
                        startTime,
                        token);
                }
            }
        }
    }
}
