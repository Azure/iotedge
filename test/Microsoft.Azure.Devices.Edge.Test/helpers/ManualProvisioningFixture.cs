// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public class X509ManualProvisioningFixture : ManualProvisioningFixture
    {
        public X509Thumbprint thumbprint { get; set; }
        public EdgeDevice device { get; set; }

        public X509ManualProvisioningFixture()
            :base()
        {
        }

        [OneTimeSetUp]
        public async Task X509ProvisionEdgeAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;
                        DateTime startTime = DateTime.Now;

                        this.thumbprint = this.CreateSelfSignedCertificateThumbprint();

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                        Context.Current.DeviceId + "-x509",
                        this.iotHub,
                        AuthenticationType.SelfSigned,
                        thumbprint,
                        token);

                        Context.Current.DeleteList.TryAdd(device.Id, device);

                        this.device = device;

                        await base.ManuallyProvisionEdgeAsync(device, startTime, token);
                    }
                },
            "Completed edge manual provisioning");

        }

        private X509Thumbprint CreateSelfSignedCertificateThumbprint()
        {
            return new X509Thumbprint()
            {
                PrimaryThumbprint = "9991572f0a02bdc7c89fc032b95d79aca18ef7a3",
                SecondaryThumbprint = "9991572f0a02bdc7c89fc032b95d79aca18ef7a4"
            };
        }
    }

    public class SasManualProvisioningFixture : ManualProvisioningFixture
    {
        public SasManualProvisioningFixture()
            : base()
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

                        await base.ManuallyProvisionEdgeAsync(device, startTime, token);
                    }
                },
            "Completed edge manual provisioning");

        }
    }

    public class ManualProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;
        protected readonly IotHub iotHub;
        protected EdgeRuntime runtime;

        public ManualProvisioningFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
            this.runtime = new EdgeRuntime(
                Context.Current.DeviceId,
                Context.Current.EdgeAgentImage,
                Context.Current.EdgeHubImage,
                Context.Current.Proxy,
                Context.Current.Registries,
                Context.Current.OptimizeForPerformance,
                this.iotHub);
        }

        
        public async Task ManuallyProvisionEdgeAsync(EdgeDevice device, DateTime startTime, CancellationToken token)
        {
            // NUnit's [Timeout] attribute isn't supported in .NET Standard
            // and even if it were, it doesn't run the teardown method when
            // a test times out. We need teardown to run, to remove the
            // device registration from IoT Hub and stop the daemon. So
            // we have our own timeout mechanism.

            IotHubConnectionStringBuilder builder =
                IotHubConnectionStringBuilder.Create(device.ConnectionString);

            await this.daemon.ConfigureAsync(
                config =>
                {
                    config.SetDeviceConnectionString(device.ConnectionString);
                    config.Update();
                    return Task.FromResult((
                        "with connection string for device '{Identity}'",
                        new object[] { builder.DeviceId }));
                },
                token);

            try
            {
                await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent(device.Id, this.iotHub);
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(token);
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
    }
}
