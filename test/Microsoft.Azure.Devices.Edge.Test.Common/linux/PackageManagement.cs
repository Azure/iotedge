// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class PackageManagement
    {
        Lazy<Task<LinuxStandardBase>> lsbTask =
            new Lazy<Task<LinuxStandardBase>>(
                async () =>
                {
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(1000);
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

        string forceInstallConfigCmd = null;
        public string ForceInstallConfigCmd
        {
            get
            {
                if (this.forceInstallConfigCmd == null)
                {
                    LinuxStandardBase lsb = this.GetLsbResult().Result;
                    switch (lsb.PackageExtension)
                    {
                        case SupportedPackageExtension.Deb:
                            this.forceInstallConfigCmd = "dpkg --force-confnew -i";
                            break;
                        case SupportedPackageExtension.Rpm:
                            this.forceInstallConfigCmd = "rpm -iv --replacepkgs";
                            break;
                        default:
                            throw new NotImplementedException($"Undefined forceInstallConfigCmd for '{lsb.PackageExtension}'");
                    }
                }

                return this.forceInstallConfigCmd;
            }
        }

        string installCmd = null;
        public string InstallCmd
        {
            get
            {
                if (this.installCmd == null)
                {
                    LinuxStandardBase lsb = this.GetLsbResult().Result;
                    switch (lsb.PackageExtension)
                    {
                        case SupportedPackageExtension.Deb:
                            this.installCmd = "install -f";
                            break;
                        case SupportedPackageExtension.Rpm:
                            this.installCmd = "install -y";
                            break;
                        default:
                            throw new NotImplementedException($"Undefined installCmd for '{lsb.PackageExtension}'");
                    }
                }

                return this.installCmd;
            }
        }

        string iotedgeServices = null;
        public string IotedgeServices
        {
            get
            {
                if (this.iotedgeServices == null)
                {
                    LinuxStandardBase lsb = this.GetLsbResult().Result;
                    switch (lsb.PackageExtension)
                    {
                        case SupportedPackageExtension.Deb:
                            this.iotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
                            break;
                        case SupportedPackageExtension.Rpm:
                            this.iotedgeServices = "iotedge.service";
                            break;
                        default:
                            throw new NotImplementedException($"Undefined iotedgeServices for '{lsb.PackageExtension}'");
                    }
                }

                return this.iotedgeServices;
            }
        }

        public string Os
        {
            get
            {
                LinuxStandardBase lsb = this.GetLsbResult().Result;
                return lsb.Os;
            }
        }

        public SupportedPackageExtension PackageExtension
        {
            get
            {
                LinuxStandardBase lsb = this.GetLsbResult().Result;
                return lsb.PackageExtension;
            }
        }

        string packageTool = null;
        public string PackageTool
        {
            get
            {
                if (this.packageTool == null)
                {
                    LinuxStandardBase lsb = this.GetLsbResult().Result;
                    switch (lsb.PackageExtension)
                    {
                        case SupportedPackageExtension.Deb:
                            this.packageTool = "apt-get";
                            break;
                        case SupportedPackageExtension.Rpm:
                            this.packageTool = "yum";
                            break;
                        default:
                            throw new NotImplementedException($"Undefined packageTool for '{lsb.PackageExtension}'");
                    }
                }

                return this.packageTool;
            }
        }

        string uninstallCmd = null;
        public string UninstallCmd
        {
            get
            {
                if (this.uninstallCmd == null)
                {
                    LinuxStandardBase lsb = this.GetLsbResult().Result;
                    switch (lsb.PackageExtension)
                    {
                        case SupportedPackageExtension.Deb:
                            this.uninstallCmd = "purge --yes";
                            break;
                        case SupportedPackageExtension.Rpm:
                            this.uninstallCmd = "remove -y --remove-leaves";
                            break;
                        default:
                            throw new NotImplementedException($"Undefined UninstallCmd for '{lsb.PackageExtension}'");
                    }
                }

                return this.uninstallCmd;
            }
        }

        public string Version
        {
            get
            {
                LinuxStandardBase lsb = this.GetLsbResult().Result;
                return lsb.Version;
            }
        }

        public enum SupportedPackageExtension
        {
            Deb,
            Rpm,
            None
        }

        public PackageManagement()
        {
        }

        private async Task<LinuxStandardBase> GetLsbResult()
        {
            return await this.lsbTask.Value;
        }

        public string[] GetInstallCommandsFromLocal(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            string[] packages = Directory
                .GetFiles(path, $"*.{this.PackageExtension.ToString().ToLower()}")
                .Where(p => !p.Contains("debug"))
                .ToArray();

            switch (this.PackageExtension)
            {
                case PackageManagement.SupportedPackageExtension.Deb:
                    return new[]
                        {
                            "set -e",
                            $"{this.ForceInstallConfigCmd} {string.Join(' ', packages)}",
                            $"{this.PackageTool} {this.InstallCmd}"
                        };
                case PackageManagement.SupportedPackageExtension.Rpm:
                    return new[]
                        {
                            "set -e",
                            $"{this.PackageTool} {this.InstallCmd} {string.Join(' ', packages)}",
                            "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                        };
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on for '.{this.PackageExtension}'");
            }
        }

        public string[] GetInstallCommandsFromMicrosoftProd()
        {
            switch (this.PackageExtension)
            {
                case PackageManagement.SupportedPackageExtension.Deb:
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
                case PackageManagement.SupportedPackageExtension.Rpm:
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
    }
}