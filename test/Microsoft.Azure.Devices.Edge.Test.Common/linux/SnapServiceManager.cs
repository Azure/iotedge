// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    class SnapServiceManager : IServiceManager
    {
        enum ServiceStatus
        {
            Running,
            Stopped
        }

        public async Task StartAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"start azure-iot-edge.aziot-edged", token);
            await this.WaitForStatusAsync(ServiceStatus.Running, token);
        }

        public async Task StopAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"stop azure-iot-edge.aziot-edged", token);
            await this.WaitForStatusAsync(ServiceStatus.Stopped, token);
        }

        public async Task ConfigureAsync(CancellationToken token)
        {
            var config = await File.ReadAllTextAsync(this.ConfigurationPath(), token);
            // Turn off verbose logging when setting config to avoid logging sensitive information, like docker
            // registry credentials.
            Log.Verbose($"Calling 'snap set azure-iot-edge raw-config=...' with contents of {this.ConfigurationPath()}");
            await Process.RunAsync(
                "snap",
                new string[] { "set", "azure-iot-edge", $"raw-config={config}" },
                token,
                logCommand: false,
                logOutput: true);

            // `snap set azure-iot-edge raw-config=...` calls `iotedge config apply`, which, for snaps, only restarts
            // aziot-edged, not identityd, keyd, certd, or tpmd. The identity service components need to refresh their
            // config, so we'll force-restart them.
            await Process.RunAsync("snap", "stop azure-iot-edge.aziot-edged", token);
            await Process.RunAsync("snap", "restart azure-iot-identity", token);
            await Process.RunAsync("snap", "start azure-iot-edge.aziot-edged", token);
        }

        public string ConfigurationPath() =>
            "/var/snap/azure-iot-identity/current/shared/config/aziot/config-e2e.toml";
        public string GetCliName() => "azure-iot-edge.iotedge";

        async Task WaitForStatusAsync(ServiceStatus desired, CancellationToken token)
        {
            Func<string, bool> stateMatchesDesired = desired switch
            {
                ServiceStatus.Running => s => s == "active",
                ServiceStatus.Stopped => s => s == "inactive",
                _ => throw new NotImplementedException($"No handler for {desired}"),
            };

            while (true)
            {
                string[] output = await Process.RunAsync("snap", "services azure-iot-edge.aziot-edged", token);
                string state = output.Last().Split(" ", StringSplitOptions.RemoveEmptyEntries)[2];
                if (stateMatchesDesired(state))
                {
                    break;
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }
}
