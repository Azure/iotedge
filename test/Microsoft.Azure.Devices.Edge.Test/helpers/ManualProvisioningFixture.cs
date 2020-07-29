// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    // NUnit's [Timeout] attribute isn't supported in .NET Standard
    // and even if it were, it doesn't run the teardown method when
    // a test times out. We need teardown to run, to remove the
    // device registration from IoT Hub and stop the daemon. So
    // we have our own timeout mechanism.
    public class ManualProvisioningFixture : BaseFixture
    {
        protected readonly IotHub iotHub;
        protected IEdgeDaemon daemon;

        public ManualProvisioningFixture(string connectionString, string eventHubEndpoint)
        {
            this.iotHub = new IotHub(connectionString, eventHubEndpoint, Context.Current.Proxy);
        }

        public ManualProvisioningFixture() : this(Context.Current.ConnectionString, Context.Current.EventHubEndpoint) { }

        [OneTimeSetUp]
        protected async Task BeforeAllTestsAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            Option<Registry> bootstrapRegistry = Option.Maybe(Context.Current.Registries.First());
            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(
                Context.Current.InstallerPath,
                Context.Current.EdgeAgentBootstrapImage,
                bootstrapRegistry,
                cts.Token);
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
