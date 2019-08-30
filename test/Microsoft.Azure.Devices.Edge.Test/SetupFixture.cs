// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
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

        public SetupFixture()
        {
            this.daemon = OsPlatform.Current.CreateEdgeDaemon(Context.Current.InstallerPath);
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

                    // Install IoT Edge, and do some basic configuration
                    using (var cts = new CancellationTokenSource(Context.Current.SetupTimeout))
                    {
                        CancellationToken token = cts.Token;

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
        public void CloseLogger() => Log.CloseAndFlush();
    }
}
