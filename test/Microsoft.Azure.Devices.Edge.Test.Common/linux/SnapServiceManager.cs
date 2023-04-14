// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class SnapServiceManager : ServiceManager
    {
        readonly string[] names =
        {
            "azure-iot-identity.keyd",
            "azure-iot-identity.certd",
            "azure-iot-identity.identityd",
            "azure-iot-edge.aziot-edged"
        };

        public override async Task StartAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"start {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServicesStatus.Running, token);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"stop {string.Join(' ', this.names)}", token);
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
                        ServicesStatus.Stopped => s => s == "inactive",
                        _ => throw new NotImplementedException($"No handler for {desired}"),
                    };

                    string[] output = await Process.RunAsync("snap", $"services {service}", token);
                    if (stateMatchesDesired(output.Last().Split(" ")[2]))
                    {
                        break;
                    }

                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }
        }
    }
}
