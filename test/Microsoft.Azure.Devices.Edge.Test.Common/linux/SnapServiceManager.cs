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
            string[] output = await Process.RunAsync("snap", $"get {this.SnapService(service)} raw-config", token);
            return string.Join("\n", output);
        }

        public Task WriteConfigurationAsync(Service service, string config, CancellationToken token) =>
            Process.RunAsync("snap", $"set {this.SnapService(service)} raw-config='{config}'", token);

        public async Task ResetConfigurationAsync(Service service, CancellationToken token)
        {
            string svc = this.SnapService(service);
            string path = $"{this.ConfigPath(service)}/config.toml.default";

            await Process.RunAsync("snap", $"set {svc} raw-config=\"$(cat {path})\"", token);

            string principalsPath = this.GetPrincipalsPath(service);
            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);
                Directory.CreateDirectory(principalsPath);
                OsPlatform.Current.SetOwner(principalsPath, "root", "755");
                Serilog.Log.Verbose($"Cleared {principalsPath}");
            }
        }

        public string GetPrincipalsPath(Service service) => $"{this.ConfigPath(service)}/config.d";

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
                    string state = output.Last().Split(" ", StringSplitOptions.RemoveEmptyEntries)[2];
                    if (stateMatchesDesired(state))
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

        string ConfigPath(Service service) => service switch
        {
            Service.Keyd => "/snap/azure-iot-identity/current/etc/aziot/keyd",
            Service.Certd => "/snap/azure-iot-identity/current/etc/aziot/certd",
            Service.Identityd => "/snap/azure-iot-identity/current/etc/aziot/identityd",
            Service.Edged => "/snap/azure-iot-edge/current/etc/aziot/edged",
            _ => throw new NotImplementedException($"Unrecognized service '{service.ToString()}'"),
        };
    }
}