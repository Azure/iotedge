// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Events;

    [SetUpFixture]
    public class SetupFixture
    {
        readonly IEdgeDaemon daemon;
        readonly IotHub iotHub;
        EdgeDevice device;

        public SetupFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
        }

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            await Profiler.Run(
                async () =>
                {
                    // Set up logging
                    LogEventLevel consoleLevel = Context.Current.Verbose
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;
                    var loggerConfig = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .WriteTo.NUnit(consoleLevel);
                    Context.Current.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
                    Log.Logger = loggerConfig.CreateLogger();

                    // Install IoT Edge
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

                        await this.daemon.UninstallAsync(token);
                        await this.daemon.InstallAsync(
                            this.device.ConnectionString,
                            Context.Current.PackagePath,
                            Context.Current.Proxy,
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
                "Completed end-to-end test setup");
        }

        [OneTimeTearDown]
        public Task AfterAllAsync() => TryFinally.DoAsync(
            () => Profiler.Run(
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
                "Completed end-to-end test teardown"),
            () =>
            {
                Log.CloseAndFlush();
            });
    }
}
