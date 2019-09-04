// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Events;

    [SetUpFixture]
    public class SetupFixture
    {
        readonly IEdgeDaemon daemon;
        bool deleteDevice;
        IotHub iotHub;

        public SetupFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
            this.deleteDevice = false;
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

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;

                        // Learn whether the requested deviceId already exists in IoT Hub. If it
                        // doesn't then the tests will create it, and should delete it too.
                        this.deleteDevice = !(await EdgeDevice.GetIdentityAsync(
                            Context.Current.DeviceId,
                            this.iotHub,
                            token)).HasValue;

                        // Install IoT Edge, and do some basic configuration
                        await this.daemon.UninstallAsync(token);
                        await this.daemon.InstallAsync(
                            Context.Current.PackagePath,
                            Context.Current.Proxy,
                            token);

                        await Context.Current.Proxy.ForEachAsync(
                            async proxy =>
                            {
                                await this.daemon.ConfigureAsync(
                                    config =>
                                    {
                                        config.AddHttpsProxy(proxy);
                                        config.Update();
                                        return Task.FromResult((
                                            "with proxy '{ProxyUri}'",
                                            new object[] { proxy.ToString() }));
                                    },
                                    token);
                            });
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
                        if (this.deleteDevice)
                        {
                            Option<EdgeDevice> device = await EdgeDevice.GetIdentityAsync(
                                Context.Current.DeviceId,
                                this.iotHub,
                                token);
                            await device.ForEachAsync(d => d.DeleteIdentityAsync(token));
                        }
                    }
                },
                "Completed end-to-end test teardown"),
            () =>
            {
                Log.CloseAndFlush();
            });
    }
}
