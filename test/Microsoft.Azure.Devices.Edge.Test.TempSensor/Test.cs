// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.TempSensor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;
    using Serilog.Events;

    public class Test
    {
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
                    Log.Information("Running tempSensor test");
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

                            var daemon = Platform.CreateEdgeDaemon(args.InstallerPath);
                            await daemon.UninstallAsync(token);
                            await daemon.InstallAsync(
                                device.ConnectionString,
                                args.PackagesPath,
                                args.Proxy,
                                token);
                            await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                            var agent = new EdgeAgent(device.Id, iotHub);
                            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                            await agent.PingAsync(token);

                            // ** test
                            var config = new EdgeConfiguration(device.Id, args.AgentImage, iotHub);
                            args.Registry.ForEach(
                                r => config.AddRegistryCredentials(r.address, r.username, r.password));
                            config.AddEdgeHub(args.HubImage);
                            args.Proxy.ForEach(p => config.AddProxy(p));
                            config.AddTempSensor(args.SensorImage);
                            await config.DeployAsync(token);

                            var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                            var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                            await EdgeModule.WaitForStatusAsync(
                                new[] { hub, sensor },
                                EdgeModuleStatus.Running,
                                token);
                            await sensor.WaitForEventsReceivedAsync(token);

                            var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                            await sensorTwin.UpdateDesiredPropertiesAsync(
                                new
                                {
                                    properties = new
                                    {
                                        desired = new
                                        {
                                            SendData = true,
                                            SendInterval = 10
                                        }
                                    }
                                },
                                token);
                            await sensorTwin.WaitForReportedPropertyUpdatesAsync(
                                new
                                {
                                    properties = new
                                    {
                                        reported = new
                                        {
                                            SendData = true,
                                            SendInterval = 10
                                        }
                                    }
                                },
                                token);

                            // ** teardown
                            await daemon.StopAsync(token);
                            await device.MaybeDeleteIdentityAsync(token);
                        },
                        "Completed tempSensor test");
                }
            }
            catch (OperationCanceledException e)
            {
                Log.Error(e, "Cancelled tempSensor test after {Timeout} minutes", args.Timeout.TotalMinutes);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed tempSensor test");
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
            public string SensorImage;
            public Option<(string address, string username, string password)> Registry;
            public TimeSpan Timeout;
            public bool Verbose;
            public Option<string> LogFile;
        }
    }
}
