// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        protected EdgeRuntime runtime;
        protected CertificateAuthority ca;

        public SasManualProvisioningFixture()
            : base()
        {
        }

        public SasManualProvisioningFixture(string connectionString, string eventHubEndpoint)
            : base(connectionString, eventHubEndpoint)
        {
        }

        protected override Task BeforeTestTimerStarts() => this.SasProvisionEdgeAsync();

        protected virtual async Task SasProvisionEdgeAsync()
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

                await this.ConfigureDaemonAsync(
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
                    token);

                if (Context.Current.NestedEdge)
                {
                    await this.SetUpCertificatesAsync();
                }
            }
        }

        public async Task SetUpCertificatesAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    (string, string, string) rootCa =
                        Context.Current.RootCaKeys.Expect(() => new InvalidOperationException("Missing root CA keys"));
                    string caCertScriptPath =
                        Context.Current.CaCertScriptPath.Expect(() => new InvalidOperationException("Missing CA cert script path"));

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        DateTime startTime = DateTime.Now;
                        CancellationToken token = cts.Token;
                        string deviceId = this.runtime.DeviceId;

                        try
                        {
                            this.ca = await CertificateAuthority.CreateAsync(
                                deviceId,
                                rootCa,
                                caCertScriptPath,
                                token);

                            CaCertificates caCert = await this.ca.GenerateCaCertificatesAsync(deviceId, token);
                            this.ca.EdgeCertificates = caCert;

                            await this.daemon.ConfigureAsync(
                                config =>
                                {
                                    config.SetCertificates(caCert);
                                    config.Update();
                                    return Task.FromResult(("with edge certificates", Array.Empty<object>()));
                                },
                                token);

                            await this.runtime.DeployConfigurationAsync(token);
                        }

                        // ReSharper disable once RedundantCatchClause
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            await NUnitLogs.CollectAsync(startTime, token);
                        }
                    }
                },
                "Completed custom certificate setup");
        }

        [OneTimeTearDown]
        public async Task RemoveCertificatesAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                    {
                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                config.RemoveCertificates();
                                config.Update();
                                return Task.FromResult(("without edge certificates", Array.Empty<object>()));
                            },
                            cts.Token);
                    }
                },
                "Completed custom certificate teardown");
        }
    }
}
