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
        public string ForceInstallConfigCmd { get; private set; }
        public string IotedgeServices { get; private set; }
        public string Os { get; private set; }
        public SupportedPackageExtension PackageExtension { get; private set; }
        public string PackageTool { get; private set; }
        public string UninstallCmd { get; private set; }
        public string Version { get; private set; }

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

            LinuxStandardBase lsb = await this.GetLsbResult();

            string[] packages = Directory
                .GetFiles(path, $"*.{lsb.PackageExtension.ToString().ToLower()}")
                .Where(p => !p.Contains("debug"))
                .ToArray();

            switch (lsb.PackageExtension)
            {
                case SupportedPackageExtension.Deb:
                    this.InitializeDebProperties(lsb);

                    return new[]
                        {
                            "set -e",
                            $"{this.ForceInstallConfigCmd} {string.Join(' ', packages)}",
                            $"{this.PackageTool} install -f"
                        };
                case SupportedPackageExtension.Rpm:
                    this.InitializeRpmProperties(lsb);

                    return new[]
                        {
                            "set -e",
                            $"{this.PackageTool} install -y {string.Join(' ', packages)}",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.PackageExtension}'");
            }
        }

        public async Task<string[]> GetInstallCommandsFromMicrosoftProdAsync()
        {
            LinuxStandardBase lsb = await this.GetLsbResult();

            switch (lsb.PackageExtension)
            {
                case SupportedPackageExtension.Deb:
                    this.InitializeDebProperties(lsb);

                    // Based on instructions at:
                    // https://github.com/MicrosoftDocs/azure-docs/blob/058084949656b7df518b64bfc5728402c730536a/articles/iot-edge/how-to-install-iot-edge-linux.md
                    // TODO: 8/30/2019 support curl behind a proxy
                    return new[]
                        {
                            $"curl https://packages.microsoft.com/config/{this.Os}/{this.Version}/multiarch/prod.list > /etc/apt/sources.list.d/microsoft-prod.list",
                            "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                            $"{this.PackageTool} update",
                            $"{this.PackageTool} install --yes iotedge"
                        };
                case SupportedPackageExtension.Rpm:
                    this.InitializeRpmProperties(lsb);

                    return new[]
                        {
                            $"{this.ForceInstallConfigCmd} https://packages.microsoft.com/config/{this.Os}/{this.Version}/packages-microsoft-prod.rpm",
                            $"{this.PackageTool} updateinfo",
                            $"{this.PackageTool} install --yes iotedge",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.PackageExtension}'");
            }
        }

        void InitializeDebProperties(LinuxStandardBase lsb)
        {
            this.Os = lsb.Os;
            this.Version = lsb.Version;
            this.PackageExtension = lsb.PackageExtension;
            this.PackageTool = "apt-get";
            this.UninstallCmd = "purge --yes";
            this.ForceInstallConfigCmd = "dpkg --force-confnew -i";
            this.IotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
        }

        void InitializeRpmProperties(LinuxStandardBase lsb)
        {
            this.Os = lsb.Os;
            this.Version = lsb.Version;
            this.PackageExtension = lsb.PackageExtension;
            this.PackageTool = "yum";
            this.UninstallCmd = "remove -y --remove-leaves";
            this.ForceInstallConfigCmd = "rpm -iv --replacepkgs";
            this.IotedgeServices = "iotedge.service";
        }
    }
}