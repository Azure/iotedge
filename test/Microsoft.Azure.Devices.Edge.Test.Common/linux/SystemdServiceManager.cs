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
            string template = path + ".default";

            if (File.Exists(path))
            {
                File.Move(path, backup, true);
            }

            File.Copy(template, path, true);
            OsPlatform.Current.SetOwner(path, Owner(service), "644");

            Serilog.Log.Verbose($"Reset {path} to {template}");

            string principalsPath = this.GetPrincipalsPath(service);
            if (Directory.Exists(principalsPath))
            {
                Directory.Delete(principalsPath, true);
                Directory.CreateDirectory(principalsPath);
                OsPlatform.Current.SetOwner(principalsPath, Owner(service), "755");
                Serilog.Log.Verbose($"Cleared {principalsPath}");
            }

            return Task.CompletedTask;
        }

        public string GetPrincipalsPath(Service service) =>
            Path.Combine(Path.GetDirectoryName(this.ConfigurationPath(service)), "config.d");

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

        string ConfigurationPath(Service service) => service switch
        {
            Service.Keyd => "/etc/aziot/keyd/config.toml",
            Service.Certd => "/etc/aziot/certd/config.toml",
            Service.Identityd => "/etc/aziot/identityd/config.toml",
            Service.Edged => "/etc/aziot/edged/config.toml",
            _ => throw new NotImplementedException($"Unrecognized service '{service.ToString()}'"),
        };

        static string Owner(Service service) => service switch
        {
            Service.Keyd => "aziotks",
            Service.Certd => "aziotcs",
            Service.Identityd => "aziotid",
            Service.Edged => "iotedge",
            _ => throw new NotImplementedException(),
        };
    }
}
