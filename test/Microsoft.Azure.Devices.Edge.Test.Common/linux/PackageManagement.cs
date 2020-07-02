// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class PackageManagement
    {
        public string Os { get; }
        public string Version { get; }
        public string PackageTool { get; }
        public SupportedPackageExtension PackageExtension { get; }
        public string ForceInstallConfigCmd { get; }
        public string InstallCmd { get; }
        public string IotedgeServices { get; }
        public string UninstallCmd { get; }

        public enum SupportedPackageExtension
        {
            Deb,
            Rpm
        }

        public static async Task<PackageManagement> CreateAsync()
        {
            var tokenSource = new CancellationTokenSource();
            string[] platformInfo = await Process.RunAsync("lsb_release", "-sir", tokenSource.Token);
            PackageManagement packageManagement = new PackageManagement(platformInfo);
            return packageManagement;
        }

        PackageManagement(string[] platformInfo)
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
                    this.Os = "ubuntu";
                    this.Version = version;
                    this.PackageTool = "apt-get";
                    this.PackageExtension = SupportedPackageExtension.Deb;
                    this.ForceInstallConfigCmd = "dpkg --force-confnew -i";
                    this.InstallCmd = "install -f";
                    this.UninstallCmd = "purge --yes";
                    this.IotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
                    break;
                case "Raspbian":
                    this.Os = "debian";
                    this.Version = "stretch";
                    this.PackageTool = "apt-get";
                    this.PackageExtension = SupportedPackageExtension.Deb;
                    this.ForceInstallConfigCmd = "dpkg --force-confnew -i";
                    this.InstallCmd = "install -f";
                    this.UninstallCmd = "purge --yes";
                    this.IotedgeServices = "iotedge.mgmt.socket iotedge.socket iotedge.service";
                    break;
                case "CentOS":
                    this.Os = os.ToLower();
                    this.Version = version.Split('.')[0];
                    this.PackageTool = "yum";
                    this.PackageExtension = SupportedPackageExtension.Rpm;
                    this.ForceInstallConfigCmd = "rpm -iv --replacepkgs";
                    this.InstallCmd = "install -y";
                    this.UninstallCmd = "remove -y --remove-leaves";
                    this.IotedgeServices = "iotedge.service";

                    if (this.Version != "7")
                    {
                        throw new NotImplementedException($"Don't know how to install daemon on operating system '{this.Os} {this.Version}'");
                    }

                    break;
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
            }
        }
    }
}