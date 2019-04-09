using System;
using System.Threading;
using System.Threading.Tasks;
using common;

namespace temp_sensor
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                // ** setup
                var daemon = new SecurityDaemon(args[0]);
                await daemon.UninstallAsync(cts.Token).ConfigureAwait(false);
                // InstallSecurityDaemon();
                // StopSecurityDaemon();
                // var identity = CreateEdgeDeviceIdentity();
                // ConfigureSecurityDaemon(identity);
                // StartSecurityDaemon();
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
