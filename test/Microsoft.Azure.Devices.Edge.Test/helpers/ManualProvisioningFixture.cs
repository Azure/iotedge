// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

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

        [OneTimeSetUp]
        public async Task ManuallyProvisionEdgeAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        // NUnit's [Timeout] attribute isn't supported in .NET Standard
                        // and even if it were, it doesn't run the teardown method when
                        // a test times out. We need teardown to run, to remove the
                        // device registration from IoT Hub and stop the daemon. So
                        // we have our own timeout mechanism.
                        DateTime startTime = DateTime.Now;
                        CancellationToken token = cts.Token;

                        EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            token);
                        Context.Current.DeleteList.TryAdd(device.Id, device);

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
                },
                "Completed edge manual provisioning");
        }
    }
}
