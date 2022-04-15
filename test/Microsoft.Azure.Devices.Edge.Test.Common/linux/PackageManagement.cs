// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public enum SupportedPackageExtension
    {
        Deb,
        Rpm
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
            this.IotedgeServices = string.Join(
                " ",
                "aziot-keyd.service",
                "aziot-certd.service",
                "aziot-identityd.service",
                "aziot-edged.service");
        }

        public string[] GetInstallCommandsFromLocal(string path)
        {
            string[] packages = Directory
                .GetFiles(path, $"*.{this.packageExtension.ToString().ToLower()}")
                .Where(p => !p.Contains("debug") && !p.Contains("devel"))
                .ToArray();

            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    "set -e",
                    $"apt-get install -y --option DPkg::Lock::Timeout=600 {string.Join(' ', packages)}",
                    $"apt-get install -f --option DPkg::Lock::Timeout=600"
                },
                SupportedPackageExtension.Rpm => new[]
                {
                    "set -e",
                    $"rpm --nodeps -i {string.Join(' ', packages)}",
                    "pathToSystemdConfig=$(systemctl cat aziot-edged | head -n 1)",
                    "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                    "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                    "sudo systemctl daemon-reload"
                },
                _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageExtension}'"),
            };
        }

        public string[] GetInstallCommandsFromMicrosoftProd(Option<Uri> proxy)
        {
            // we really support only two options for now.
            string repository = this.os.ToLower() switch
            {
                "ubuntu" => $"https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list",
                "debian" => $"https://packages.microsoft.com/config/debian/stretch/multiarch/prod.list",
                _ => throw new NotImplementedException($"Don't know how to install daemon for '{this.os}'"),
            };

            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    $"curl {repository} > /etc/apt/sources.list.d/microsoft-prod.list",
                    "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                    $"apt-get update",
                    $"apt-get install --option DPkg::Lock::Timeout=600 --yes aziot-edge"
                },
                SupportedPackageExtension.Rpm => new[]
                {
                    $"rpm -iv --replacepkgs https://packages.microsoft.com/config/{this.os}/{this.version}/packages-microsoft-prod.rpm",
                    $"yum updateinfo",
                    $"yum install --yes aziot-edge",
                    "pathToSystemdConfig=$(systemctl cat aziot-edge | head -n 1)",
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
                "dpkg --purge aziot-edge",
                "dpkg --purge aziot-identity-service",
                "dpkg --purge iotedge",
                "dpkg --purge libiothsm-std",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            SupportedPackageExtension.Rpm => new[]
            {
                "yum remove -y aziot-edge",
                "yum remove -y aziot-identity-service",
                "yum remove -y iotedge",
                "yum remove -y libiothsm-std",
                "yum autoremove -y",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            _ => throw new NotImplementedException($"Don't know how to uninstall daemon on for '.{this.packageExtension}'")
        };
    }
}
