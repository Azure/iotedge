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

        public Task<string> ReadConfigurationAsync(Service service, CancellationToken token) =>
            File.ReadAllTextAsync(this.ConfigurationPath(service), token);

        public async Task WriteConfigurationAsync(Service service, string config, CancellationToken token)
        {
            string path = this.ConfigurationPath(service);

            FileAttributes attr = File.GetAttributes(path);
            File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);

            await File.WriteAllTextAsync(path, config);

            if (attr != 0)
            {
                File.SetAttributes(path, attr);
            }
        }

        public Task ResetConfigurationAsync(Service service, CancellationToken token)
        {
            string path = this.ConfigurationPath(service);
            string backup = path + ".backup";
            string template = this.TemplatePath(service);

            if (File.Exists(path))
            {
                File.Move(path, backup, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(template, path, true);
            OsPlatform.Current.SetOwner(path, this.GetOwner(service), "644");

            Serilog.Log.Verbose($"Reset {path} to {template}");

            string principalsPath = this.GetPrincipalsPath(service);
            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);
                Directory.CreateDirectory(principalsPath);
                OsPlatform.Current.SetOwner(principalsPath, this.GetOwner(service), "755");
                Serilog.Log.Verbose($"Cleared {principalsPath}");
            }

            return Task.CompletedTask;
        }

        public string GetPrincipalsPath(Service service) =>
            Path.Combine(Path.GetDirectoryName(this.ConfigurationPath(service)), "config.d");

        public string GetOwner(Service _) => "root";

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

        string TemplatePath(Service service) => service switch
        {
            Service.Keyd => "/snap/azure-iot-identity/current/etc/aziot/keyd/config.toml.default",
            Service.Certd => "/snap/azure-iot-identity/current/etc/aziot/certd/config.toml.default",
            Service.Identityd => "/snap/azure-iot-identity/current/etc/aziot/identityd/config.toml.default",
            Service.Edged => "/snap/azure-iot-edge/current/etc/aziot/edged/config.toml.default",
            _ => throw new NotImplementedException(),
        };

        string ConfigurationPath(Service service) => service switch
        {
            Service.Keyd => "/var/snap/azure-iot-identity/current/shared/config/aziot/keyd/config.d/00-super.toml",
            Service.Certd => "/var/snap/azure-iot-identity/current/shared/config/aziot/certd/config.d/00-super.toml",
            Service.Identityd => "/var/snap/azure-iot-identity/current/shared/config/aziot/identityd/config.d/00-super.toml",
            Service.Edged => "/var/snap/azure-iot-edge/current/shared/config/aziot/edged/config.d/00-super.toml",
            _ => throw new NotImplementedException(),
        };
    }
}
