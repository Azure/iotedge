// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PackageManagement
    {
        Lazy<Task<LinuxStandardBase>> lsbTask =
            new Lazy<Task<LinuxStandardBase>>(
                async () =>
                {
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(10000);
                    string[] platformInfo = await Process.RunAsync("lsb_release", "-sir", cts.Token);
                    if (platformInfo.Length == 1)
                    {
                        platformInfo = platformInfo[0].Split(' ');
                    }

                    string os = platformInfo[0].Trim();
                    string version = platformInfo[1].Trim();
                    SupportedPackageExtension packageExtension = SupportedPackageExtension.None;

                    switch (os)
                    {
                        case "Ubuntu":
                            os = "ubuntu";
                            packageExtension = SupportedPackageExtension.Deb;
                            break;
                        case "Raspbian":
                            os = "debian";
                            version = "stretch";
                            packageExtension = SupportedPackageExtension.Deb;
                            break;
                        case "CentOS":
                            os = os.ToLower();
                            version = version.Split('.')[0];
                            packageExtension = SupportedPackageExtension.Rpm;

                            if (version != "7")
                            {
                                throw new NotImplementedException($"Don't know how to install daemon on operating system '{os} {version}'");
                            }

                            break;
                        default:
                            throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
                    }

                    return new LinuxStandardBase(os, version, packageExtension);
                });

        // Note:
        //   These properties are initialized upon GetInstallCommandsFromLocalAsync()
        //   or GetInstallCommandsFromMicrosoftProdAsync()
        string forceInstallConfigCmd = null;
        string iotedgeServices = null;
        string os = null;
        SupportedPackageExtension packageExtension = SupportedPackageExtension.None;
        string packageTool = null;
        string uninstallCmd = null;
        string version = null;
        bool isInitilized = false;

        public PackageManagement()
        {
        }

        private async Task<LinuxStandardBase> GetLsbResult()
        {
            return await this.lsbTask.Value;
        }

        public async Task<string[]> GetInstallCommandsFromLocalAsync(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            await this.InitializePropertiesAsync();

            string[] packages = Directory
                .GetFiles(path, $"*.{this.packageExtension.ToString().ToLower()}")
                .Where(p => !p.Contains("debug"))
                .ToArray();

            switch (this.packageExtension)
            {
                case SupportedPackageExtension.Deb:
                    return new[]
                        {
                            "set -e",
                            $"{this.forceInstallConfigCmd} {string.Join(' ', packages)}",
                            $"{this.packageTool} install -f"
                        };
                case SupportedPackageExtension.Rpm:
                    return new[]
                        {
                            "set -e",
                            $"{this.packageTool} install -y {string.Join(' ', packages)}",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'");
            }
        }

        public async Task<string[]> GetInstallCommandsFromMicrosoftProdAsync()
        {
            await this.InitializePropertiesAsync();

            switch (this.packageExtension)
            {
                case SupportedPackageExtension.Deb:
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
                case SupportedPackageExtension.Rpm:
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

        public async Task<string> GetPackageToolAsync()
        {
            await this.InitializePropertiesAsync();
            return this.packageTool;
        }

        public async Task<string> GetUninstallCmdAsync()
        {
            await this.InitializePropertiesAsync();
            return this.uninstallCmd;
        }

        public async Task<string> GetIotedgeServicesAsync()
        {
            await this.InitializePropertiesAsync();
            return this.iotedgeServices;
        }

        async Task InitializePropertiesAsync()
        {
            LinuxStandardBase lsb = await this.GetLsbResult();

            if (!this.isInitilized)
            {
                switch (lsb.PackageExtension)
                {
                    case SupportedPackageExtension.Deb:
                        this.InitializeDebProperties(lsb);
                        break;
                    case SupportedPackageExtension.Rpm:
                        this.InitializeRpmProperties(lsb);
                        break;
                    default:
                        throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'");
                }

                this.isInitilized = true;
            }
        }

        void InitializeDebProperties(LinuxStandardBase lsb)
        {
            this.os = lsb.Os;
            this.version = lsb.Version;
            this.packageExtension = lsb.PackageExtension;
            this.packageTool = "apt-get";
            this.uninstallCmd = "purge --yes";
            this.forceInstallConfigCmd = "dpkg --force-confnew -i";
            this.iotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
        }

        void InitializeRpmProperties(LinuxStandardBase lsb)
        {
            this.os = lsb.Os;
            this.version = lsb.Version;
            this.packageExtension = lsb.PackageExtension;
            this.packageTool = "yum";
            this.uninstallCmd = "remove -y --remove-leaves";
            this.forceInstallConfigCmd = "rpm -iv --replacepkgs";
            this.iotedgeServices = "iotedge.service";
        }
    }
}