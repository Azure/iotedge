// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Serilog;

    // NUnit's [Timeout] attribute isn't supported in .NET Standard
    // and even if it were, it doesn't run the teardown method when
    // a test times out. We need teardown to run, to remove the
    // device registration from IoT Hub and stop the daemon. So
    // we have our own timeout mechanism.
    public class ManualProvisioningFixture : BaseFixture
    {
        protected readonly IEdgeDaemon daemon;
        protected readonly IotHub iotHub;

        public ManualProvisioningFixture()
        {
            (string serverAddress, string username, string password) firstRegistry = Context.Current.Registries.First();
            (string image, string serverAddress, string username, string password) bootstrapAgentInfo =
                (Context.Current.EdgeAgentBootstrapImage, firstRegistry.serverAddress, firstRegistry.username, firstRegistry.password);
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath, bootstrapAgentInfo);
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
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
