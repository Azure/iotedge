// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public abstract class PackageManagement
    {
        public readonly string Os;
        public readonly string Version;
        public readonly string ExtensionName;
        public string IotedgeServices { get; }

        public PackageManagement(string extensionName, string os, string version)
        {
            this.Os = os;
            this.Version = version;
            this.ExtensionName = extensionName;
            this.IotedgeServices = string.Join(
                " ",
                "aziot-keyd.service",
                "aziot-certd.service",
                "aziot-identityd.service",
                "aziot-edged.service");
        }

        public abstract string[] GetInstallCommandsFromLocalWithPackages(string[] packages);
        public abstract string[] GetInstallCommandsFromMicrosoftProd();
        public abstract string[] GetUninstallCommands();

        public string[] GetPackages(string path)
        {
            return Directory
                .GetFiles(path, $"*.{this.ExtensionName.ToLower()}")
                .Where(p => !p.Contains("debug") && !p.Contains("devel"))
                .ToArray();
        }

        public string[] GetInstallCommandsFromLocal(string path)
        {
            string[] packages = this.GetPackages(path);
            return this.GetInstallCommandsFromLocalWithPackages(packages);
        }
    }

    public class DebPackageCommands : PackageManagement
    {
        public DebPackageCommands(string os, string version)
            : base("deb", os, version)
        {
        }

        public override string[] GetInstallCommandsFromLocalWithPackages(string[] packages)
        {
            return new[]
            {
                "set -e",
                $"apt-get install -y {string.Join(' ', packages)}",
                $"apt-get install -f"
            };
        }

        public override string[] GetInstallCommandsFromMicrosoftProd()
        {
            string repository = this.Os.ToLower() switch
            {
                "ubuntu" => this.Version == "18.04" ? "https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list" : "https://packages.microsoft.com/config/ubuntu/20.04/prod.list",
                "debian" => $"https://packages.microsoft.com/config/debian/stretch/multiarch/prod.list",
                _ => throw new NotImplementedException($"Don't know how to install daemon for '{this.Os}'"),
            };

            return new[]
            {
                $"curl {repository} > /etc/apt/sources.list.d/microsoft-prod.list",
                "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                $"apt-get update",
                $"apt-get install --yes aziot-edge"
            };
        }

        public override string[] GetUninstallCommands()
        {
            return new[]
            {
                "dpkg --purge aziot-edge",
                "dpkg --purge aziot-identity-service",
                "dpkg --purge iotedge",
                "dpkg --purge libiothsm-std",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            };
        }
    }

    public class NoPackageManagerRpmPackageCommands : PackageManagement
    {
        public NoPackageManagerRpmPackageCommands(string os, string version)
            : base("rpm", os, version)
        {
        }

        public override string[] GetInstallCommandsFromLocalWithPackages(string[] packages)
        {
            return new[]
            {
                $"rpm --nodeps -i {string.Join(' ', packages)}"
            };
        }

        public override string[] GetInstallCommandsFromMicrosoftProd()
        {
            throw new NotImplementedException($"Don't know how to install daemon on for generic rpm install");
        }

        public override string[] GetUninstallCommands()
        {
            return new[]
            {
                "if rpm -qa aziot-edge | grep -q aziot-edge; then rpm -e aziot-edge; fi",
                "if rpm -qa aziot-identity-service | grep -q aziot-identity-service; then rpm -e aziot-identity-service; fi",
                "if rpm -qa iotedge | grep -q iotedge; then rpm -e iotedge; fi",
                "if rpm -qa libiothsm-std | grep -q libiothsm-std; then rpm -e libiothsm-std; fi",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            };
        }
    }

    public class YumPackageManagerRpmPackagesCommands : PackageManagement
    {
        public YumPackageManagerRpmPackagesCommands(string os, string version)
            : base("rpm", os, version)
        {
        }

        public override string[] GetInstallCommandsFromLocalWithPackages(string[] packages)
        {
            return new[]
            {
                $"rpm --nodeps -i {string.Join(' ', packages)}"
            };
        }

        public override string[] GetInstallCommandsFromMicrosoftProd()
        {
            return new[]
            {
                $"rpm -iv --replacepkgs https://packages.microsoft.com/config/{this.Os}/{this.Version}/packages-microsoft-prod.rpm",
                $"yum updateinfo",
                $"yum install --yes aziot-edge",
                "pathToSystemdConfig=$(systemctl cat aziot-edge | head -n 1)",
                "sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf",
                "sudo mv -f ~/override.conf ${pathToSystemdConfig#?}",
                "sudo systemctl daemon-reload"
            };
        }

        public override string[] GetUninstallCommands()
        {
            return new[]
            {
                "yum remove -y --remove-leaves aziot-edge",
                "yum remove -y --remove-leaves aziot-identity-service",
                "yum remove -y --remove-leaves iotedge",
                "yum remove -y --remove-leaves libiothsm-std",
                "systemctl restart docker" // we can remove after this is fixed (https://github.com/moby/moby/issues/23302)
            };
        }
    }
}
