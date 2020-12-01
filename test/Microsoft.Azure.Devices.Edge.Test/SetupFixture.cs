// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
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
        IEdgeDaemon daemon;

        private string[] configFiles =
        {
            "/etc/aziot/keyd/config.toml",
            "/etc/aziot/certd/config.toml",
            "/etc/aziot/identityd/config.toml",
            "/etc/aziot/edged/config.yaml"
        };

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            CancellationToken token = cts.Token;
            Option<Registry> bootstrapRegistry = Option.Maybe(Context.Current.Registries.FirstOrDefault());

            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(
                Context.Current.InstallerPath,
                Context.Current.EdgeAgentBootstrapImage,
                bootstrapRegistry,
                token);

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
                    await this.daemon.UninstallAsync(token);
                    await this.daemon.InstallAsync(Context.Current.PackagePath, Context.Current.Proxy, token);

                    // Create a directory for the tests to store certs, keys, etc.
                    Directory.CreateDirectory("/etc/aziot/e2e_tests");

                    // Backup any existing service config files.
                    foreach(string file in this.configFiles)
                    {
                        if(File.Exists(file))
                        {
                            File.Copy(file, file + ".backup", true);
                        }
                    }

                    await this.daemon.ConfigureAsync(
                        config =>
                        {
                            var msgBuilder = new StringBuilder();
                            var props = new List<object>();

                            string hostname = Dns.GetHostName();
                            config.SetDeviceHostname(hostname);
                            msgBuilder.Append("with hostname '{hostname}'");
                            props.Add(hostname);

                            Context.Current.ParentHostname.ForEach(parentHostname =>
                            {
                                config.SetParentHostname(parentHostname);
                                msgBuilder.AppendLine(", parent hostname '{parentHostname}'");
                                props.Add(parentHostname);
                            });

                            Context.Current.Proxy.ForEach(proxy =>
                            {
                                config.AddHttpsProxy(proxy);
                                msgBuilder.AppendLine(", proxy '{ProxyUri}'");
                                props.Add(proxy.ToString());
                            });

                            config.Update();

                            return Task.FromResult((msgBuilder.ToString(), props.ToArray()));
                        },
                        token,
                        restart: false);
                },
                "Completed end-to-end test setup");
        }

        [OneTimeTearDown]
        public Task AfterAllAsync() => TryFinally.DoAsync(
            () => Profiler.Run(
                async () =>
                {
                    using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                    CancellationToken token = cts.Token;
                    await this.daemon.StopAsync(token);
                    foreach (EdgeDevice device in Context.Current.DeleteList.Values)
                    {
                        await device.MaybeDeleteIdentityAsync(token);
                    }

                    // Remove packages installed by this run.
                    await this.daemon.UninstallAsync(token);

                    // Delete test certs, keys, etc.
                    Directory.Delete("/etc/aziot/e2e_tests", true);

                    // Restore backed up config files.
                    foreach(string file in this.configFiles)
                    {
                        string backupFile = file + ".backup";

                        if(File.Exists(backupFile))
                        {
                            File.Copy(backupFile, file, true);
                            File.Delete(backupFile);
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
