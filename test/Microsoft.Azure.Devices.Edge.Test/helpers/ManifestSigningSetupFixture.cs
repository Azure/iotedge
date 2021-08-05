// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;

    public class ManifestSigningSetupFixture : SasManualProvisioningFixture
    {
        Option<string> manifestSigningTrustBundlePath;

        protected override Task BeforeTestTimerStarts() => this.SasProvisionEdgeAsync();

        protected override async Task SasProvisionEdgeAsync(bool withCerts = false)
        {
            // It creates a Sas Provisioned device with a manifest trust bundle
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
                Log.Information($"Device ID {this.device.Id}");

                Context.Current.DeleteList.TryAdd(this.device.Id, this.device);

                this.runtime = new EdgeRuntime(
                    this.device.Id,
                    Context.Current.EdgeAgentImage,
                    Context.Current.EdgeHubImage,
                    Context.Current.EdgeProxy,
                    Context.Current.Registries,
                    Context.Current.OptimizeForPerformance,
                    this.IotHub);

                if (Context.Current.EnableManifestSigning)
                {
                    // This is a temporary solution see ticket: 9288683
                    if (!Context.Current.ISA95Tag)
                    {
                        TestCertificates testCerts;
                        (testCerts, this.ca) = await TestCertificates.GenerateCertsAsync(this.device.Id, token);

                        await this.ConfigureDaemonAsync(
                            config =>
                            {
                                testCerts.AddCertsToConfigForManifestSigning(config, this.manifestSigningTrustBundlePath);

                                config.SetManualSasProvisioning(this.IotHub.Hostname, Context.Current.ParentHostname, this.device.Id, this.device.SharedAccessKey);

                                config.Update();
                                return Task.FromResult((
                                    "with connection string for device '{Identity}'",
                                    new object[] { this.device.Id }));
                            },
                            this.device,
                            startTime,
                            token);
                    }
                }
            }
        }

        public void SetManifestTrustBundle(Option<string> manifestSigningRootCaPath) => this.manifestSigningTrustBundlePath = manifestSigningRootCaPath;
    }
}
