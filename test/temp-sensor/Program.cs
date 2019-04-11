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
        // args[2] - path to IotEdgeSecurityDaemon.ps1
        static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                CancellationToken token = cts.Token;

                // ** setup
                var identity = new EdgeDeviceIdentity(args[0], args[1]);
                await identity.GetOrCreateAsync(token);

                var daemon = new EdgeDaemon(args[2], identity);
                await daemon.UninstallAsync(token);
                await daemon.InstallAsync(token);
                await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                var agent = new EdgeAgent();
                await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                await agent.PingAsync(args[1], args[0], token);

                // ** test
                // var config = CreateEdgeConfiguration();
                // AddTempSensor(config);
                // DeployEdgeConfiguration(config);
                // EnsureConfigurationIsDeployed();
                // EnsureTempSensorIsRunning();
                // EnsureTempSensorEventsAreSent();
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
