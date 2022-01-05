// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Serilog;

    public class ManifestTrustSetupFixture : SasManualProvisioningFixture
    {
        protected override Task BeforeTestTimerStarts() => this.SasProvisionEdgeAsync();

        protected override Task AfterTestTimerEnds() => this.daemon.ConfigureAsync(
                        config =>
                        {
                            config.RemoveManifestTrustBundle();

                            config.Update();
                            return Task.FromResult((
                                "Removed Manifest Trust Bundle", new object[] { }));
                        },
                        new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

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
            }
        }
    }
}
