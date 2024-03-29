// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using NUnit.Framework;
    using NestedEdgeConfig = Microsoft.Azure.Devices.Edge.Test.Common.EdgeDevice.NestedEdgeConfig;

    // NUnit's [Timeout] attribute isn't supported in .NET Standard
    // and even if it were, it doesn't run the teardown method when
    // a test times out. We need teardown to run, to remove the
    // device registration from IoT Hub and stop the daemon. So
    // we have our own timeout mechanism.
    public class ManualProvisioningFixture : BaseFixture
    {
        public IotHub IotHub { get; }

        protected IEdgeDaemon daemon;
        protected CertificateAuthority ca;

        public ManualProvisioningFixture()
        {
            this.IotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.TestRunnerProxy);
        }

        [OneTimeSetUp]
        protected async Task BeforeAllTestsAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(Context.Current.PackagePath, cts.Token);
            this.cli = this.daemon.GetCli();
        }

        protected async Task ConfigureDaemonAsync(
            Func<DaemonConfiguration, Task<(string, object[])>> config,
            EdgeDevice device,
            DateTime startTime,
            CancellationToken token)
        {
            await this.daemon.ConfigureAsync(config, token);

            try
            {
                var agent = new EdgeAgent(device.Id, this.IotHub);
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, this.cli, token);
                await agent.PingAsync(token);
            }

            // ReSharper disable once RedundantCatchClause
            catch
            {
                throw;
            }
            finally
            {
                using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                await NUnitLogs.CollectAsync(startTime, this.cli, cts.Token);
            }
        }

        protected NestedEdgeConfig GetNestedEdgeConfig(IotHub iotHub)
        {
            return new NestedEdgeConfig(
                iotHub,
                Context.Current.NestedEdge,
                Context.Current.ParentDeviceId,
                Context.Current.ParentHostname,
                Context.Current.Hostname);
        }
    }
}
