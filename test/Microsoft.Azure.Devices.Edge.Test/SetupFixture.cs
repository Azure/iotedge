// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Net;
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
        IotHub iotHub;

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

                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;

                        // Install IoT Edge, and do some basic configuration
                        await this.daemon.UninstallAsync(token);
                        await this.daemon.InstallAsync(
                            Context.Current.PackagePath,
                            Context.Current.Proxy,
                            token);

                        await this.daemon.ConfigureAsync(
                            config =>
                            {
                                var msg = string.Empty;
                                var props = new object[] { };

                                config.SetDeviceHostname(Dns.GetHostName());
                                Context.Current.Proxy.ForEach(proxy =>
                                {
                                    config.AddHttpsProxy(proxy);
                                    msg = "with proxy '{ProxyUri}'";
                                    props = new object[] { proxy.ToString() };
                                });
                                config.Update();

                                return Task.FromResult((msg, props));
                            },
                            token,
                            restart: false);
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
                        foreach (EdgeDevice device in Context.Current.DeleteList.Values)
                        {
                            await device.MaybeDeleteIdentityAsync(token);
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
