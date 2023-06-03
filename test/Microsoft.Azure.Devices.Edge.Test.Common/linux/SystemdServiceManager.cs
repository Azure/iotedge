// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class SystemdServiceManager : IServiceManager
    {
        enum ServiceStatus
        {
            Running,
            Stopped
        }

        readonly string[] names = { "aziot-keyd", "aziot-certd", "aziot-identityd", "aziot-edged" };

        public async Task StartAsync(CancellationToken token)
        {
            await Process.RunAsync("systemctl", $"start {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServiceStatus.Running, token);
        }

        public async Task StopAsync(CancellationToken token)
        {
            await Process.RunAsync("systemctl", $"stop {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServiceStatus.Stopped, token);
        }

        public async Task ConfigureAsync(CancellationToken token)
        {
            await Process.RunAsync("iotedge", "config apply", token);
        }

        public string ConfigurationPath() => "/etc/aziot/config.toml";
        public string GetCliName() => "iotedge";

        async Task WaitForStatusAsync(ServiceStatus desired, CancellationToken token)
        {
            foreach (string service in this.names)
            {
                while (true)
                {
                    Func<string, bool> stateMatchesDesired = desired switch
                    {
                        ServiceStatus.Running => s => s == "active",
                        ServiceStatus.Stopped => s => s == "inactive" || s == "failed",
                        _ => throw new NotImplementedException($"No handler for {desired}"),
                    };

                    string[] output = await Process.RunAsync("systemctl", $"-p ActiveState show {service}", token);
                    if (stateMatchesDesired(output.First().Split("=").Last()))
                    {
                        break;
                    }

                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }
        }
    }
}
