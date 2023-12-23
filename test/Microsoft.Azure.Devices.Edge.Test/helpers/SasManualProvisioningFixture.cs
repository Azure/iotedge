// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;
        protected EdgeDevice device;

        protected override Task BeforeTestTimerStarts() => this.SasProvisionEdgeAsync();

        protected virtual async Task SasProvisionEdgeAsync(bool withCerts = false)
        {
            using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
            {
                CancellationToken token = cts.Token;
                DateTime startTime = DateTime.Now;

                this.device = await EdgeDevice.GetOrCreateIdentityAsync(
                    Context.Current.DeviceId.GetOrElse(DeviceId.Current.Generate()),
                    this.GetNestedEdgeConfig(this.IotHub),
                    this.IotHub,
                    AuthenticationType.Sas,
                    null,
                    token);

                Context.Current.DeleteList.TryAdd(this.device.Id, this.device);

                this.runtime = new EdgeRuntime(
                    this.device.Id,
                    Context.Current.EdgeAgentImage,
                    Context.Current.EdgeHubImage,
                    Context.Current.EdgeProxy,
                    Context.Current.Registries,
                    Context.Current.OptimizeForPerformance,
                    this.IotHub);

                // This is a temporary solution see ticket: 9288683
                if (!Context.Current.ISA95Tag)
                {
                    (var certs, this.ca) = await TestCertificates.GenerateEdgeCaCertsAsync(
                        this.device.Id,
                        this.daemon.GetCertificatesPath(),
                        token);

                    await this.ConfigureDaemonAsync(
                        async config =>
                        {
                            config.SetCertificates(certs);
                            config.SetManualSasProvisioning(
                                this.IotHub.Hostname,
                                Context.Current.ParentHostname,
                                this.device.Id,
                                this.device.SharedAccessKey);

                            await config.UpdateAsync(token);
                            return ("with connection string for device '{Identity}'", new object[] { this.device.Id });
                        },
                        this.device,
                        startTime,
                        token);
                }
            }
        }
    }
}
