// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class SystemdServiceManager : ServiceManager
    {
        readonly string[] names = { "aziot-keyd", "aziot-certd", "aziot-identityd", "aziot-edged" };

        public override async Task StartAsync(CancellationToken token)
        {
            await Process.RunAsync("systemctl", $"start {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServicesStatus.Running, token);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await Process.RunAsync("systemctl", $"stop {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServicesStatus.Stopped, token);
        }

        async Task WaitForStatusAsync(ServicesStatus desired, CancellationToken token)
        {
            foreach (string service in this.names)
            {
                while (true)
                {
                    Func<string, bool> stateMatchesDesired = desired switch
                    {
                        ServicesStatus.Running => s => s == "active",
                        ServicesStatus.Stopped => s => s == "inactive" || s == "failed",
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
