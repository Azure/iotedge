// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class SystemInfo
    {
        public SystemInfo(string operatingSystemType, string architecture, string version, ProvisioningInfo provisioning, string serverVersion, string kernelVersion, string operatingSystem, int numCpus, string virtualized)
        {
            this.OperatingSystemType = operatingSystemType;
            this.Architecture = architecture;
            this.Version = version;
            this.Provisioning = provisioning;
            this.ServerVersion = serverVersion;
            this.KernelVersion = kernelVersion;
            this.OperatingSystem = operatingSystem;
            this.NumCpus = numCpus;
            this.Virtualized = virtualized;
        }

        public SystemInfo(string operatingSystemType, string architecture, string version)
            : this(operatingSystemType, architecture, version, ProvisioningInfo.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty)
        {
        }

        public string OperatingSystemType { get; }

        public string Architecture { get; }

        public string Version { get; }

        public ProvisioningInfo Provisioning { get; }

        public string ServerVersion { get; }

        public string KernelVersion { get; }

        public string OperatingSystem { get; }

        public int NumCpus { get; }

        public string Virtualized { get; }

        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty, string.Empty);
    }
}
