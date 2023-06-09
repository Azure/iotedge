// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

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
            // 'iotedge config apply' expects this directory to exist, but it doesn't for snaps
            Directory.CreateDirectory(
                "/var/snap/azure-iot-identity/current/shared/config/aziot/edged/config.d");

            await Process.RunAsync("azure-iot-edge.iotedge", "config apply", token);
            await Process.RunAsync("snap", "restart azure-iot-identity.identityd azure-iot-edge.aziot-edged", token);
        }

        public string ConfigurationPath() =>
            "/var/snap/azure-iot-identity/current/shared/config/aziot/config.toml";
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
                string[] output = await Process.RunAsync("snap", $"services azure-iot-edge.aziot-edged", token);
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
