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
        readonly string[] names =
        {
            // "azure-iot-identity.keyd",
            // "azure-iot-identity.certd",
            // "azure-iot-identity.identityd",
            "azure-iot-edge.aziot-edged"
        };

        public async Task StartAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"start {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServiceStatus.Running, token);
        }

        public async Task StopAsync(CancellationToken token)
        {
            await Process.RunAsync("snap", $"stop {string.Join(' ', this.names)}", token);
            await this.WaitForStatusAsync(ServiceStatus.Stopped, token);
        }

        public async Task<string> ReadConfigurationAsync(Service service, CancellationToken token)
        {
            string[] output = await Process.RunAsync("snap", $"get {SnapService(service)} raw-config", token);
            return string.Join("\n", output);
        }

        public Task WriteConfigurationAsync(Service service, string config, CancellationToken token) =>
            Process.RunAsync("snap", $"set {SnapService(service)} raw-config='{config}'", token);

        public string GetPrincipalsPath(Service service) =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                service.ToString(),
                "config.d");

        async Task WaitForStatusAsync(ServiceStatus desired, CancellationToken token)
        {
            foreach (string service in this.names)
            {
                while (true)
                {
                    Func<string, bool> stateMatchesDesired = desired switch
                    {
                        ServiceStatus.Running => s => s == "active",
                        ServiceStatus.Stopped => s => s == "inactive",
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

        string SnapService(Service service) => service switch
        {
            Service.Keyd => "azure-iot-identity.keyd",
            Service.Certd => "azure-iot-identity.certd",
            Service.Identityd => "azure-iot-identity.identityd",
            Service.Edged => "azure-iot-edge.aziot-edged",
            _ => throw new NotImplementedException($"Unrecognized service '{service.ToString()}'"),
        };
    }
}
