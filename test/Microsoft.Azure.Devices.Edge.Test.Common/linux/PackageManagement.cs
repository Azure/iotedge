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
        Rpm,
        Snap
    }

    public class PackageManagement
    {
        readonly string os;
        readonly string version;
        readonly SupportedPackageExtension packageExtension;

        public PackageManagement(string os, string version, SupportedPackageExtension extension)
        {
            this.os = os;
            this.version = version;
            this.packageExtension = extension;
        }

        public SupportedPackageExtension PackageExtension => this.packageExtension;

        public string[] GetInstallCommandsFromLocal(string path)
        {
            string[] packages = Directory
                .GetFiles(path, $"*.{this.PackageExtension.ToString().ToLower()}")
                .Where(p => !p.Contains("debuginfo")
                    && !p.Contains("dbgsym")
                    && !p.Contains("devel")
                    && !p.Contains("src.rpm"))
                .ToArray();

            return this.PackageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    "set -e",
                    $"apt-get install -y --option DPkg::Lock::Timeout=600 {string.Join(' ', packages)}",
                    $"apt-get install -f --option DPkg::Lock::Timeout=600"
                },
                SupportedPackageExtension.Rpm => this.os switch
                {
                    "rhel" => new[]
                    {
                        "set -e",
                        $"sudo dnf -y install {string.Join(' ', packages)}",
                        "pathToSystemdConfig=$(systemctl cat aziot-edged | head -n 1)",
                        "pathToOverride=$(dirname ${pathToSystemdConfig#?})/aziot-edged.service.d",
                        "sudo mkdir $pathToOverride",
                        "echo -e \"[Service]\nRestart=no\" >  ~/override.conf",
                        "sudo mv -f ~/override.conf ${pathToOverride}/overrides.conf",
                        "sudo systemctl daemon-reload"
                    },
                    "mariner" => new[]
                    {
                        "set -e",
                        $"sudo dnf -y install {string.Join(' ', packages)}",
                        "pathToSystemdConfig=$(systemctl cat aziot-edged | head -n 1)",
                        "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                        "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                        "sudo systemctl daemon-reload"
                    },
                    _ => throw new NotImplementedException($"RPM packaging is set up only for RHEL and Mariner, current OS '.{this.os}'"),
                },
                SupportedPackageExtension.Snap => new[]
                {
                    "set -e",
                    $"snap install {string.Join(' ', packages)} --dangerous",
                    "snap connect azure-iot-identity:hostname-control",
                    "snap connect azure-iot-identity:log-observe",
                    "snap connect azure-iot-identity:mount-observe",
                    "snap connect azure-iot-identity:system-observe",
                    // There isn't a TPM in this setup, so don't connect it
                    // "snap connect azure-iot-identity:tpm",
                    "snap connect azure-iot-edge:home",
                    "snap connect azure-iot-edge:hostname-control",
                    "snap connect azure-iot-edge:log-observe",
                    "snap connect azure-iot-edge:mount-observe",
                    "snap connect azure-iot-edge:system-observe",
                    "snap connect azure-iot-edge:run-iotedge",
                    "snap connect azure-iot-edge:aziotctl-executables azure-iot-identity:aziotctl-executables",
                    "snap connect azure-iot-edge:identity-service azure-iot-identity:identity-service",
                    "snap connect azure-iot-edge:docker docker:docker-daemon",
                    "snap alias azure-iot-edge.iotedge iotedge"
                },
                _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.PackageExtension}'"),
            };
        }

        public string[] GetInstallCommandsFromMicrosoftProd(Option<Uri> proxy)
        {
            return this.PackageExtension switch
            {
                SupportedPackageExtension.Deb => new[]
                {
                    $"apt-get update",
                    $"apt-get install --option DPkg::Lock::Timeout=600 --yes aziot-edge"
                },
                SupportedPackageExtension.Rpm => this.os switch
                {
                    "rhel" => new[]
                    {
                        $"sudo rpm -iv --replacepkgs https://packages.microsoft.com/config/{this.os}/{this.version}/packages-microsoft-prod.rpm",
                        $"sudo dnf updateinfo",
                        $"sudo dnf install -y aziot-edge",
                        "pathToSystemdConfig=$(systemctl cat aziot-edged | head -n 1)",
                        "pathToOverride=$(dirname ${pathToSystemdConfig#?})/aziot-edged.service.d",
                        "sudo mkdir $pathToOverride",
                        "echo -e \"[Service]\nRestart=no\" >  ~/override.conf",
                        "sudo mv -f ~/override.conf ${pathToOverride}/overrides.conf",
                        "sudo systemctl daemon-reload"
                    },
                    _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.os}'")
                },
                _ => throw new NotImplementedException($"Don't know how to install daemon on for '.{this.PackageExtension}'"),
            };
        }

        public string[] GetUninstallCommands() => this.PackageExtension switch
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
                "pathToSystemdConfig=$(systemctl cat aziot-edged | head -n 1)",
                "pathToOverride=$(dirname ${pathToSystemdConfig#?})/aziot-edged.service.d",
                "sudo rm -f ${pathToOverride}/overrides.conf",
                "yum remove -y aziot-edge",
                "yum remove -y aziot-identity-service",
                "yum remove -y iotedge",
                "yum remove -y libiothsm-std",
                "yum autoremove -y",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            SupportedPackageExtension.Snap => new string[]
            {
                "snap remove --purge azure-iot-edge",
                "snap remove --purge azure-iot-identity",
                "rm -r -f /etc/aziot",
                "snap restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            },
            _ => throw new NotImplementedException($"Don't know how to uninstall daemon on for '.{this.PackageExtension}'")
        };
    }
}
