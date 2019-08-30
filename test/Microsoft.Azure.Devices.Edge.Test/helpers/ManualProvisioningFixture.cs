﻿// Copyright (c) Microsoft. All rights reserved.
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
        EdgeDevice device;

        public ManualProvisioningFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
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

                        Assert.IsNull(this.device);
                        this.device = await EdgeDevice.GetOrCreateIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            token);

                        IotHubConnectionStringBuilder builder =
                            IotHubConnectionStringBuilder.Create(this.device.ConnectionString);

                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                config.SetDeviceConnectionString(this.device.ConnectionString);
                                config.Update();
                                return Task.FromResult((
                                    "with connection string for device '{Identity}'",
                                    new object[] { builder.DeviceId }));
                            },
                            token);

                        try
                        {
                            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                            var agent = new EdgeAgent(this.device.Id, this.iotHub);
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

        [OneTimeTearDown]
        public Task StopEdgeAsync() => Profiler.Run(
            async () =>
            {
                using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                {
                    CancellationToken token = cts.Token;

                    await this.daemon.StopAsync(token);

                    Assert.IsNotNull(this.device);
                    await this.device.MaybeDeleteIdentityAsync(token);
                }
            },
            "Completed end-to-end test teardown");
    }
}
