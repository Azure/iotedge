// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
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
        IEdgeDaemon daemon;

        [OneTimeSetUp]
        public async Task BeforeAllAsync()
        {
            using var cts = new CancellationTokenSource(Context.Current.SetupTimeout);
            CancellationToken token = cts.Token;

            // Set up logging
            LogEventLevel consoleLevel = Context.Current.Verbose
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.NUnit(consoleLevel);
            Context.Current.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
            Log.Logger = loggerConfig.CreateLogger();

            this.daemon = await OsPlatform.Current.CreateEdgeDaemonAsync(Context.Current.PackagePath, token);

            await Profiler.Run(
                async () =>
                {
                    // Install IoT Edge, and do some basic configuration
                    await this.daemon.UninstallAsync(token);

                    // Delete directories used by previous installs.
                    string[] directories = { "/run/aziot", "/var/lib/aziot", "/etc/aziot" };

                    foreach (string directory in directories)
                    {
                        if (Directory.Exists(directory))
                        {
                            Directory.Delete(directory, true);
                            Log.Verbose($"Deleted {directory}");
                        }
                    }

                    await this.daemon.InstallAsync(Context.Current.EdgeProxy, token);

                    string certsPath = this.daemon.GetCertificatesPath();
                    if (Directory.Exists(certsPath))
                    {
                        Directory.Delete(certsPath, true);
                    }

                    Directory.CreateDirectory(certsPath);

                    await this.daemon.ConfigureAsync(
                        async config =>
                        {
                            var msgBuilder = new StringBuilder();
                            var props = new List<object>();

                            string hostname = Context.Current.Hostname.GetOrElse(Dns.GetHostName());
                            config.SetDeviceHostname(hostname);
                            msgBuilder.Append("with hostname '{hostname}'");
                            props.Add(hostname);

                            string edgeAgent =
                                Context.Current.EdgeAgentImage.GetOrElse("mcr.microsoft.com/azureiotedge-agent:1.4");

                            Log.Verbose("Search parents");
                            Context.Current.ParentHostname.ForEach(parentHostname =>
                            {
                                Log.Verbose($"Found parent hostname {parentHostname}");
                                config.SetParentHostname(parentHostname);
                                msgBuilder.AppendLine($", parent hostname '{parentHostname}'");
                                props.Add(parentHostname);

                                edgeAgent = Regex.Replace(edgeAgent, @"\$upstream", parentHostname);
                            });

                            // The first element corresponds to the registry credentials for edge agent image
                            config.SetEdgeAgentImage(edgeAgent, Context.Current.Registries.Take(1));

                            Context.Current.EdgeProxy.ForEach(proxy =>
                            {
                                config.AddHttpsProxy(proxy);
                                msgBuilder.AppendLine(", proxy '{ProxyUri}'");
                                props.Add(proxy.ToString());
                            });

                            await config.UpdateAsync(token);

                            return (msgBuilder.ToString(), props.ToArray());
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

                    string certsPath = this.daemon.GetCertificatesPath();
                    if (Directory.Exists(certsPath))
                    {
                        Directory.Delete(certsPath, true);
                    }
                },
                "Completed end-to-end test teardown"),
            () =>
            {
                Log.CloseAndFlush();
            });
    }
}
