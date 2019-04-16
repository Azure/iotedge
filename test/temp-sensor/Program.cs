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
        static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                CancellationToken token = cts.Token;

                // ** setup
                var device = new EdgeDevice(args[0], args[1]);
                string devcs = await device.GetOrCreateIdentityAsync(token);

                var daemon = new EdgeDaemon(args[3], devcs);
                await daemon.UninstallAsync(token);
                await daemon.InstallAsync(token);
                await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent();
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(args[1], args[0], token);

                // ** test
                var config = new EdgeConfiguration();
                config.AddEdgeHub();
                config.AddTempSensor();
                await device.DeployConfigurationAsync(config, token);

                var hub = new EdgeModule("edgeHub");
                var sensor = new EdgeModule("tempSensor");
                await EdgeModule.WaitForStatusAsync(
                    new []{hub, sensor}, EdgeModuleStatus.Running, token);
                await sensor.ReceiveEventsAsync(args[2], args[0], token);
                await device.UpdateModuleTwinAsync("tempSensor", new {
                    properties = new {
                        desired = new {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                }, token);
                await device.WaitForTwinUpdatesAsync("tempSensor", new {
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
