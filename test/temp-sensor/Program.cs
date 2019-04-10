// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using common;

namespace temp_sensor
{
    class Program
    {
        // args[0] - IoT Hub connection string (TODO: should not be passed on command line?)
        // args[1] - device ID
        // args[2] - path to IotEdgeSecurityDaemon.ps1
        static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                // ** setup
                var identity = new EdgeDeviceIdentity(args[0]);
                await identity.CreateAsync(args[1], cts.Token).ConfigureAwait(false);
                var daemon = new SecurityDaemon(args[2], identity);
                await daemon.UninstallAsync(cts.Token).ConfigureAwait(false);
                await daemon.InstallAsync(cts.Token).ConfigureAwait(false);
                // EnsureSecurityDaemonIsRunning();
                // EnsureEdgeAgentIsRunning();
                // EnsureEdgeAgentIsConnectedToIotHub();

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
                // StopSecurityDaemon();
                // DeleteEdgeDeviceIdentity(identity);
            }

            return 0;
        }
    }
}
