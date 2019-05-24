// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Events;

    [SetUpFixture]
    public class SetupFixture
    {
        readonly CancellationTokenSource cts;
        readonly IEdgeDaemon daemon;
        readonly IotHub iotHub;

        public SetupFixture()
        {
            this.cts = new CancellationTokenSource(Context.Current.Timeout);
            this.daemon = Platform.CreateEdgeDaemon(
                Context.Current.InstallerPath.Expect(() => new ArgumentException()));
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
        }

        [OneTimeSetUp]
        public async Task Setup()
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
            CancellationToken token = this.cts.Token;

            EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                Context.Current.DeviceId,
                this.iotHub,
                token);

            await this.daemon.UninstallAsync(token);
            await this.daemon.InstallAsync(
                device.ConnectionString,
                Context.Current.PackagePath,
                Context.Current.Proxy,
                token);
            await this.daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

            var agent = new EdgeAgent(device.Id, this.iotHub);
            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
            await agent.PingAsync(token);
        }

        [OneTimeTearDown]
        public async Task Teardown()
        {
            CancellationToken token = this.cts.Token;

            await this.daemon.StopAsync(token);

            EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                Context.Current.DeviceId,
                this.iotHub,
                token);
            await device.MaybeDeleteIdentityAsync(token);

            this.cts.Dispose();
            Log.CloseAndFlush();
        }
    }
}
