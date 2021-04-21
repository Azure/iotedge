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
                    $"apt-get install -y {string.Join(' ', packages)}",
                    $"apt-get install -f"
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
            string debPackages = $"https://packages.microsoft.com/config/{this.os}/{this.version}/multiarch/prod.list";
            if (this.os.ToLower() == "ubuntu" && this.version == "20.04")
            {
                // Should be changed to 20.04 when we publish to that repo.
                debPackages = $"https://packages.microsoft.com/config/{this.os}/18.04/multiarch/prod.list";
            }

            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    $"curl {debPackages} > /etc/apt/sources.list.d/microsoft-prod.list",
                    "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                    $"apt-get update",
                    $"apt-get install --yes aziot-edge"
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
                "yum remove -y --remove-leaves aziot-edge",
                "yum remove -y --remove-leaves aziot-identity-service",
                "yum remove -y --remove-leaves iotedge",
                "yum remove -y --remove-leaves libiothsm-std",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            _ => throw new NotImplementedException($"Don't know how to uninstall daemon on for '.{this.packageExtension}'")
        };
    }
}
