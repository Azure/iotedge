﻿// Copyright (c) Microsoft. All rights reserved.

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
        static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                CancellationToken token = cts.Token;

                // ** setup
                var device = new EdgeDevice(args[0], args[1]);
                await device.GetOrCreateIdentityAsync(token);

                var daemon = new EdgeDaemon(device.Context.ConnectionString, args[3]);
                await daemon.UninstallAsync(token);
                await daemon.InstallAsync(token);
                await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent(device.Context.Device.Id, device.Context.IotHub);
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(token);

                // ** test
                var config = new EdgeConfiguration(device.Context.Device.Id, device.Context.IotHub);
                config.AddEdgeHub();
                config.AddTempSensor();
                await config.DeployAsync(token);

                var hub = new EdgeModule(
                    "edgeHub", device.Context.Device.Id, device.Context.IotHub);
                var sensor = new EdgeModule(
                    "tempSensor", device.Context.Device.Id, device.Context.IotHub);
                await EdgeModule.WaitForStatusAsync(
                    new []{hub, sensor}, EdgeModuleStatus.Running, token);
                await sensor.ReceiveEventsAsync(args[2], token);

                var sensorTwin = new ModuleTwin(sensor);
                await sensorTwin.UpdateDesiredPropertiesAsync(new {
                    properties = new {
                        desired = new {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                }, token);
                await sensorTwin.WaitForReportedPropertyUpdatesAsync(new {
                    properties = new {
                        reported = new {
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
        }
    }
}
