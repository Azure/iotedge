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
            string commonPackages = string.Join(
                " ",
                "aziot-keyd.service",
                "aziot-keyd.socket",
                "aziot-certd.service",
                "aziot-certd.socket",
                "aziot-identityd.service",
                "aziot-identityd.socket");

            this.os = os;
            this.version = version;
            this.packageExtension = extension;
            this.IotedgeServices = extension switch
            {
                SupportedPackageExtension.Deb => string.Join(
                    " ",
                    commonPackages,
                    "aziot-edged.mgmt.socket",
                    "aziot-edged.workload.socket",
                    "aziot-edged.service"),
                SupportedPackageExtension.Rpm => string.Join(
                    " ",
                    commonPackages,
                    "aziot-edged.service"),
                _ => throw new NotImplementedException($"Unknown package extension '.{this.packageExtension}'")
            };
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
                    $"dpkg --force-confnew -i {string.Join(' ', packages)}",
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

        public string GetDefaultEdgedConfig()
        {
            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => "/etc/aziot/edged/config.yaml.template",
                SupportedPackageExtension.Rpm => "/etc/aziot/edged/config.yaml.rpmnew",
                _ => throw new NotImplementedException($"Unknown package extension '.{this.packageExtension}'"),
            };
        }

        public string[] GetInstallCommandsFromMicrosoftProd(Option<Uri> proxy)
        {
            var curl = "curl";
            var prefix = string.Empty;
            proxy.ForEach(url =>
            {
                curl += $" -x {url}";
                prefix = $"http_proxy={url} https_proxy={url} ";
            });

            return this.packageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    // Based on instructions at:
                    // https://github.com/MicrosoftDocs/azure-docs/blob/058084949656b7df518b64bfc5728402c730536a/articles/iot-edge/how-to-install-iot-edge-linux.md
                    $"{curl} https://packages.microsoft.com/config/{this.os}/{this.version}/multiarch/prod.list > /etc/apt/sources.list.d/microsoft-prod.list",
                    $"{curl} https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                    $"{prefix}apt-get update",
                    $"{prefix}apt-get install --yes iotedge"
                },
                SupportedPackageExtension.Rpm => new[]
                {
                    // No proxy support here because our proxy test environment uses Ubuntu.
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
                "dpkg --purge libiothsm-std aziot-edge aziot-identity-service iotedge"
            },
            SupportedPackageExtension.Rpm => new[]
            {
                "yum remove -y libiothsm-std aziot-edge aziot-identity-service iotedge"
            },
            _ => throw new NotImplementedException($"Don't know how to uninstall daemon on for '.{this.packageExtension}'")
        };
    }
}
