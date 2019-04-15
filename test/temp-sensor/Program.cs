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
                var identity = new EdgeDeviceIdentity(args[0], args[1]);
                await identity.GetOrCreateAsync(token);

                var daemon = new EdgeDaemon(args[3], identity);
                await daemon.UninstallAsync(token);
                await daemon.InstallAsync(token);
                await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent();
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(args[1], args[0], token);

                // ** test
                var config = new EdgeConfiguration(args[0], args[1]);
                config.AddEdgeHub();
                config.AddTempSensor();
                await config.DeployAsync();

                var hub = new EdgeModule("edgeHub");
                var sensor = new EdgeModule("tempSensor");
                await EdgeModule.WaitForStatusAsync(
                    new []{hub, sensor}, EdgeModuleStatus.Running, token);
                await sensor.ReceiveEventsAsync(args[2], args[0], token);
                // UpdateTempSensorTwin();
                // EnsureTempSensorTwinUpdatesAreReported();

                // ** teardown
                await daemon.StopAsync(token);
                await identity.MaybeDeleteAsync(token);
            }

            return 0;
        }
    }
}
