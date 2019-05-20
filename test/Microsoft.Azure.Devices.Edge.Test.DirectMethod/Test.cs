// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.DirectMethod
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;
    using Serilog.Events;

    public class Test
    {
        public const string Name = "module-to-module direct method";

        IEdgeDaemon CreateEdgeDaemon(string installerPath) => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new Common.Windows.EdgeDaemon(installerPath)
            : new Common.Linux.EdgeDaemon() as IEdgeDaemon;

        public async Task<int> RunAsync(Args args)
        {
            LogEventLevel consoleLevel = args.Verbose
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(consoleLevel);
            args.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
            Log.Logger = loggerConfig.CreateLogger();

            try
            {
                using (var cts = new CancellationTokenSource(args.Timeout))
                {
                    Log.Information("Running test '{Name}'", Name);
                    await Profiler.Run(
                        async () =>
                        {
                            CancellationToken token = cts.Token;

                            // ** setup
                            var iotHub = new IotHub(args.ConnectionString, args.Endpoint, args.Proxy);
                            EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                                args.DeviceId,
                                iotHub,
                                token);

                            var daemon = this.CreateEdgeDaemon(args.InstallerPath);
                            await daemon.UninstallAsync(token);
                            await daemon.InstallAsync(
                                device.ConnectionString,
                                args.PackagesPath,
                                args.Proxy,
                                token);

                            await args.Proxy.Match(
                                async p =>
                                {
                                    await daemon.StopAsync(token);
                                    var yaml = new DaemonConfiguration();
                                    yaml.AddHttpsProxy(p);
                                    yaml.Update();
                                    await daemon.StartAsync(token);
                                },
                                () => daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token));

                            var agent = new EdgeAgent(device.Id, iotHub);
                            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                            await agent.PingAsync(token);

                            // ** test
                            var config = new EdgeConfiguration(device.Id, args.AgentImage, iotHub);
                            args.Registry.ForEach(
                                r => config.AddRegistryCredentials(r.address, r.username, r.password));
                            config.AddEdgeHub(args.HubImage);
                            args.Proxy.ForEach(p => config.AddProxy(p));
                            config.AddModule("methodSender", args.SenderImage)
                                .WithEnvironment(new[] { ("TargetModuleId", "methodReceiver") });
                            config.AddModule("methodReceiver", args.ReceiverImage);
                            await config.DeployAsync(token);

                            var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                            var sender = new EdgeModule("methodSender", device.Id, iotHub);
                            var receiver = new EdgeModule("methodReceiver", device.Id, iotHub);
                            await EdgeModule.WaitForStatusAsync(
                                new[] { hub, sender, receiver },
                                EdgeModuleStatus.Running,
                                token);
                            await sender.WaitForEventsReceivedAsync(token);

                            // ** teardown
                            await daemon.StopAsync(token);
                            await device.MaybeDeleteIdentityAsync(token);
                        },
                        "Completed test '{Name}'",
                        Name);
                }
            }
            catch (OperationCanceledException e)
            {
                Log.Error(e, "Cancelled test '{Name}' after {Timeout} minutes", Name, args.Timeout.TotalMinutes);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed test '{Name}'", Name);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }

        public class Args
        {
            public string DeviceId;
            public string ConnectionString;
            public string Endpoint;
            public string InstallerPath;
            public Option<string> PackagesPath;
            public Option<Uri> Proxy;
            public string AgentImage;
            public string HubImage;
            public string SenderImage;
            public string ReceiverImage;
            public Option<(string address, string username, string password)> Registry;
            public TimeSpan Timeout;
            public bool Verbose;
            public Option<string> LogFile;
        }
    }
}
