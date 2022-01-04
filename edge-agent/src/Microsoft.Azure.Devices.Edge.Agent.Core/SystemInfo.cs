// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class SystemInfo
    {
        public SystemInfo(
            string kernel, string kernelRelease, string kernelVersion,
            string operatingSystem, string operatingSystemVersion,
            string architecture, int numCpus, string virtualized, string hostOsSku,
            string boardName, string productName, string productSku, string productVersion, string systemFamily, string systemVendor,
            string version, ProvisioningInfo provisioning
        )
        {

        }

        public SystemInfo(string operatingSystemType, string architecture, string version, ProvisioningInfo provisioning, string _serverVersion, string kernelVersion, string operatingSystem, int numCpus, string virtualized)
            : this(
                operatingSystemType, kernelVersion, string.Empty,
                operatingSystem, string.Empty,
                architecture, numCpus, virtualized, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                version, provisioning
            )
        {
        }

        public SystemInfo(string operatingSystemType, string architecture, string version)
            : this(operatingSystemType, architecture, version, ProvisioningInfo.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty)
        {
        }

        public string Kernel { get; }

        public string KernelRelease { get; }

        public string KernelVersion { get; }

        public string OperatingSystem { get; }

        public string OperatingSystemVersion { get; }

        public string Architecture { get; }

        public int NumCpus { get; }

        public string Virtualized { get; }

        public string HostOsSku { get; }

        public string BoardName { get; }

        public string ProductName { get; }

        public string ProductSku { get; }

        public string ProductVersion { get; }

        public string SystemFamily { get; }

        public string SystemVendor { get; }

        public string Version { get; }

        public ProvisioningInfo Provisioning { get; }

        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty, string.Empty);
    }
}
