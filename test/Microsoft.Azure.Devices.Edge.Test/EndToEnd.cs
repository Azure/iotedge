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

    public class EndToEnd
    {
        public const string Name = "temp sensor";

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task TempSensor()
        {
            string installerPath = Context.Current.InstallerPath.Expect(() => new ArgumentException());
            string edgeAgent = Context.Current.EdgeAgent.Expect(() => new ArgumentException());
            string edgeHub = Context.Current.EdgeHub.Expect(() => new ArgumentException());
            string tempSensor = Context.Current.TempSensor.Expect(() => new ArgumentException());

            LogEventLevel consoleLevel = Context.Current.Verbose
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.NUnit(consoleLevel);
            Context.Current.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
            Log.Logger = loggerConfig.CreateLogger();

            try
            {
                using (var cts = new CancellationTokenSource(Context.Current.Timeout))
                {
                    Log.Information("Running test '{Name}'", Name);
                    await Profiler.Run(
                        async () =>
                        {
                            CancellationToken token = cts.Token;

                            // ** setup
                            var iotHub = new IotHub(Context.Current.ConnectionString, Context.Current.EventHubEndpoint, Context.Current.Proxy);
                            EdgeDevice device = await EdgeDevice.GetOrCreateIdentityAsync(
                                Context.Current.DeviceId,
                                iotHub,
                                token);

                            var daemon = Platform.CreateEdgeDaemon(installerPath);
                            await daemon.UninstallAsync(token);
                            await daemon.InstallAsync(
                                device.ConnectionString,
                                Context.Current.PackagePath,
                                Context.Current.Proxy,
                                token);
                            await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                            var agent = new EdgeAgent(device.Id, iotHub);
                            await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                            await agent.PingAsync(token);

                            // ** test
                            var config = new EdgeConfiguration(device.Id, edgeAgent, iotHub);
                            Context.Current.Registry.ForEach(
                                r => config.AddRegistryCredentials(r.address, r.username, r.password));
                            config.AddEdgeHub(edgeHub);
                            Context.Current.Proxy.ForEach(p => config.AddProxy(p));
                            config.AddModule("tempSensor", tempSensor);
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
                        "Completed test '{Name}'",
                        Name);
                }
            }
            catch (OperationCanceledException e)
            {
                Log.Error(e, "Cancelled test '{Name}' after {Timeout} minutes", Name, Context.Current.Timeout.TotalMinutes);
                throw;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed test '{Name}'", Name);
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
