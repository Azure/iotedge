// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class PackageManagement
    {
        bool isInitialized = false;

        string os;
        string version;
        string packageTool;
        SupportedPackageExtension packageExtension;
        string forceInstallConfigCmd;
        string installCmd;
        string iotedgeServices;
        string uninstallCmd;

        public enum SupportedPackageExtension
        {
            Deb,
            Rpm
        }

        public PackageManagement()
        {
            this.os = null;
            this.version = null;
            this.packageTool = null;
            this.packageExtension = 0;
            this.forceInstallConfigCmd = null;
            this.installCmd = null;
            this.iotedgeServices = null;
            this.uninstallCmd = null;
            this.isInitialized = false;
        }

        async Task CheckInitializationAsync()
        {
            if (!this.isInitialized)
            {
                var tokenSource = new CancellationTokenSource();
                string[] platformInfo = await Process.RunAsync("lsb_release", "-sir", tokenSource.Token);
                this.InitializeParameters(platformInfo);
                this.isInitialized = true;
            }
        }

        void InitializeParameters(string[] platformInfo)
        {
            if (platformInfo.Length == 1)
            {
                platformInfo = platformInfo[0].Split(' ');
            }

            string os = platformInfo[0].Trim();
            string version = platformInfo[1].Trim();

            switch (os)
            {
                case "Ubuntu":
                    this.os = "ubuntu";
                    this.version = version;
                    this.packageTool = "apt-get";
                    this.packageExtension = SupportedPackageExtension.Deb;
                    this.forceInstallConfigCmd = "dpkg --force-confnew -i";
                    this.installCmd = "install -f";
                    this.uninstallCmd = "purge --yes";
                    this.iotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
                    break;
                case "Raspbian":
                    this.os = "debian";
                    this.version = "stretch";
                    this.packageTool = "apt-get";
                    this.packageExtension = SupportedPackageExtension.Deb;
                    this.forceInstallConfigCmd = "dpkg --force-confnew -i";
                    this.installCmd = "install -f";
                    this.uninstallCmd = "purge --yes";
                    this.iotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
                    break;
                case "CentOS":
                    this.os = os.ToLower();
                    this.version = version.Split('.')[0];
                    this.packageTool = "yum";
                    this.packageExtension = SupportedPackageExtension.Rpm;
                    this.forceInstallConfigCmd = "rpm -iv --replacepkgs";
                    this.installCmd = "install -y";
                    this.uninstallCmd = "remove -y --remove-leaves";
                    this.iotedgeServices = "iotedge.service";

                    if (this.version != "7")
                    {
                        throw new NotImplementedException($"Don't know how to install daemon on operating system '{this.os} {this.version}'");
                    }

                    break;
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
            }
        }

        public async Task<string[]> GetInstallCommandsFromLocalAsync(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            await this.CheckInitializationAsync();

            string[] packages = Directory.GetFiles(path, $"*.{this.packageExtension.ToString().ToLower()}");
            for (int i = packages.Length - 1; i >= 0; --i)
            {
                if (packages[i].Contains("debug"))
                {
                    packages[i] = string.Empty;
                }
            }

            switch (this.packageExtension)
            {
                case PackageManagement.SupportedPackageExtension.Deb:
                    return new[]
                        {
                            "set -e",
                            $"{this.forceInstallConfigCmd} {string.Join(' ', packages)}",
                            $"{this.packageTool} {this.installCmd}"
                        };
                case PackageManagement.SupportedPackageExtension.Rpm:
                    return new[]
                        {
                            "set -e",
                            $"{this.packageTool} {this.installCmd} {string.Join(' ', packages)}",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'");
            }
        }

        public async Task<string[]> GetInstallCommandsFromMicrosoftProdAsync()
        {
            await this.CheckInitializationAsync();

            switch (this.packageExtension)
            {
                case PackageManagement.SupportedPackageExtension.Deb:
                    // Based on instructions at:
                    // https://github.com/MicrosoftDocs/azure-docs/blob/058084949656b7df518b64bfc5728402c730536a/articles/iot-edge/how-to-install-iot-edge-linux.md
                    // TODO: 8/30/2019 support curl behind a proxy
                    return new[]
                        {
                            $"curl https://packages.microsoft.com/config/{this.os}/{this.version}/multiarch/prod.list > /etc/apt/sources.list.d/microsoft-prod.list",
                            "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                            $"{this.packageTool} update",
                            $"{this.packageTool} install --yes iotedge"
                        };
                case PackageManagement.SupportedPackageExtension.Rpm:
                    return new[]
                        {
                            $"{this.forceInstallConfigCmd} https://packages.microsoft.com/config/{this.os}/{this.version}/packages-microsoft-prod.rpm",
                            $"{this.packageTool} updateinfo",
                            $"{this.packageTool} install --yes iotedge",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'");
            }
        }

        public async Task InternalStopAsync(CancellationToken token)
        {
            await this.CheckInitializationAsync();
            string[] output = await Process.RunAsync("systemctl", $"stop {this.iotedgeServices}", token);
            Log.Verbose(string.Join("\n", output));
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            await this.CheckInitializationAsync();
            string[] output =
                await Process.RunAsync($"{this.packageTool}", $"{this.uninstallCmd} libiothsm-std iotedge", token);
            Log.Verbose(string.Join("\n", output));
        }
    }
}