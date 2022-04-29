// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;

    public enum SupportedPackageExtension
    {
        Deb,
        RpmCentOS,
        RpmMariner
    }

    public class PackageManagement
    {
        readonly string os;
        readonly string version;
        readonly SupportedPackageExtension packageExtension;
        public string IotedgeServices { get; }

        public PackageManagement(string os, string version, SupportedPackageExtension extension)
        {
            this.os = os;
            this.version = version;
            this.packageExtension = extension;
            this.IotedgeServices = extension switch
            {
                SupportedPackageExtension.Deb => "iotedge.mgmt.socket iotedge.socket iotedge.service",
                var x when
                    x == SupportedPackageExtension.RpmCentOS ||
                    x == SupportedPackageExtension.RpmMariner => "iotedge.service",
                _ => throw new NotImplementedException($"Unknown package extension '.{this.packageExtension}'")
            };
        }

        public string[] GetInstallCommandsFromLocal(string path)
        {
            string packageExtensionString = this.packageExtension switch
            {
                SupportedPackageExtension.Deb => "deb",
                var x when
                    x == SupportedPackageExtension.RpmCentOS ||
                    x == SupportedPackageExtension.RpmMariner => "rpm",
                _ => throw new NotImplementedException($"Unknown package extension '.{this.packageExtension}'")
            };
            string[] packages = Directory
                .GetFiles(path, $"*.{packageExtensionString}")
                .Where(p => !p.Contains("debug") && !p.Contains("rust"))
                .ToArray();

            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    "set -e",
                    $"dpkg --force-confnew -i {string.Join(' ', packages)}",
                    $"apt-get install -f"
                },
                SupportedPackageExtension.RpmCentOS => new[]
                {
                    "set -e",
                    $"yum install -y {string.Join(' ', packages)}",
                    "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1)",
                    "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                    "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                    "sudo systemctl daemon-reload"
                },
                SupportedPackageExtension.RpmMariner => new[]
                {
                    "set -e",
                    $"rpm -i {string.Join(' ', packages)}",
                    "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1)",
                    "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                    "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                    "sudo systemctl daemon-reload"
                },
                _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'"),
            };
        }

        public string[] GetInstallCommandsFromMicrosoftProd()
        {
            string repository = this.os.ToLower() switch
            {
                "ubuntu" => this.version == "18.04" ? "https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list" : "https://packages.microsoft.com/config/ubuntu/20.04/prod.list",
                "debian" => $"https://packages.microsoft.com/config/debian/stretch/multiarch/prod.list",
                _ => throw new NotImplementedException($"Don't know how to install daemon for '{this.os}'"),
            };

            return this.packageExtension switch {
                SupportedPackageExtension.Deb => new[]
                {
                    // Based on instructions at:
                    // https://github.com/MicrosoftDocs/azure-docs/blob/058084949656b7df518b64bfc5728402c730536a/articles/iot-edge/how-to-install-iot-edge-linux.md
                    // TODO: 8/30/2019 support curl behind a proxy
                    $"curl {repository} > /etc/apt/sources.list.d/microsoft-prod.list",
                    "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                    $"apt-get update",
                    $"apt-get install --yes iotedge"
                },
                SupportedPackageExtension.RpmCentOS => new[]
                {
                    $"rpm -iv --replacepkgs https://packages.microsoft.com/config/{this.os}/{this.version}/packages-microsoft-prod.rpm",
                    $"yum updateinfo",
                    $"yum install --yes iotedge",
                    "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1)",
                    "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                    "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                    "sudo systemctl daemon-reload"
                },
                _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'"),
            };
        }

        public string[] GetUninstallCommands() => this.packageExtension switch
        {
            SupportedPackageExtension.Deb => new[]
            {
                "apt-get purge --yes aziot-edge aziot-identity-service libiothsm-std iotedge",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            SupportedPackageExtension.RpmCentOS => new[]
            {
                "yum remove -y --remove-leaves aziot-edge aziot-identity-service libiothsm-std iotedge",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            SupportedPackageExtension.RpmMariner => new[]
            {
                "if rpm -qa azure-iotedge | grep -q azure-iotedge; then rpm -e azure-iotedge; fi",
                "if rpm -qa libiothsm-std | grep -q libiothsm-std; then rpm -e libiothsm-std; fi",
            },
            _ => throw new NotImplementedException($"Don't know how to uninstall daemon on for '.{this.packageExtension}'")
        };
    }
}
