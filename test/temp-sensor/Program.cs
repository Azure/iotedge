// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using common;

namespace temp_sensor
{
    class Program
    {
        // args[0] - device ID
        // args[1] - IoT Hub connection string (TODO: should not be passed on command line?)
        // args[2] - Event Hub-compatible endpoint connection string (TODO: should not be passed on command line?)
        // args[3] - path to IotEdgeSecurityDaemon.ps1
        static Task<int> Main(string[] args)
        {
            return Profiler.Run(
                "Running tempSensor test",
                async () => {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                    {
                        CancellationToken token = cts.Token;

                        // ** setup
                        var iotHub = new IotHub(args[1], args[2]);
                        var device = await EdgeDevice.GetOrCreateIdentityAsync(
                            args[0], iotHub, token);

                        var daemon = new EdgeDaemon(args[3]);
                        await daemon.UninstallAsync(token);
                        await daemon.InstallAsync(device.ConnectionString, token);
                        await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                        var agent = new EdgeAgent(device.Id, iotHub);
                        await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                        await agent.PingAsync(token);

                        // ** test
                        var config = new EdgeConfiguration(device.Id, iotHub);
                        config.AddEdgeHub();
                        config.AddTempSensor();
                        await config.DeployAsync(token);

                        var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                        var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                        await EdgeModule.WaitForStatusAsync(
                            new[] { hub, sensor }, EdgeModuleStatus.Running, token);
                        await sensor.WaitForEventsReceivedAsync(token);

                        var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                        await sensorTwin.UpdateDesiredPropertiesAsync(new
                        {
                            properties = new
                            {
                                desired = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);
                        await sensorTwin.WaitForReportedPropertyUpdatesAsync(new
                        {
                            properties = new
                            {
                                reported = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);

                        // ** teardown
                        await daemon.StopAsync(token);
                        await device.MaybeDeleteIdentityAsync(token);
                    }

                    return 0;
                },
                "Completed tempSensor test"
            );
        }
    }
}
