// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;

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
                    DeviceId.Current.Generate(),
                    Context.Current.ParentDeviceId,
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

                if (Context.Current.NestedEdge || withCerts)
                {
                    await this.SetUpCertificatesAsync(token, startTime, this.runtime.DeviceId);
                }
                else
                {
                    this.ca = CertificateAuthority.GetQuickstart();
                }

                await this.ConfigureDaemonAsync(
                    config =>
                    {
                        if (Context.Current.NestedEdge || withCerts)
                        {
                            config.SetCertificates(this.ca.EdgeCertificates);
                        }

                        config.SetDeviceConnectionString(device.ConnectionString);
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
