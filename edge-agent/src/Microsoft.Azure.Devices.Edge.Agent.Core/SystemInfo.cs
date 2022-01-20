// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Text;

    using static System.Net.WebUtility;

    public class SystemInfo
    {
        public SystemInfo(string kernel, string kernelRelease, string kernelVersion, string operatingSystem, string operatingSystemVersion, string operatingSystemVariant, string operatingSystemBuild, string architecture, int numCpus, string virtualized, string productName, string systemVendor, string version, ProvisioningInfo provisioning, IDictionary<string, string> additionalProperties)
        {
            this.Kernel = kernel;
            this.KernelRelease = kernelRelease;
            this.KernelVersion = kernelVersion;
            this.OperatingSystem = operatingSystem;
            this.OperatingSystemVersion = operatingSystemVersion;
            this.OperatingSystemVariant = operatingSystemVariant;
            this.OperatingSystemBuild = operatingSystemBuild;
            this.Architecture = architecture;
            this.NumCpus = numCpus;
            this.Virtualized = virtualized;
            this.ProductName = productName;
            this.SystemVendor = systemVendor;
            this.Version = version;
            this.Provisioning = provisioning;
            this.AdditionalProperties = additionalProperties;
        }

        public SystemInfo(string operatingSystemType, string architecture, string version, ProvisioningInfo provisioning, string _serverVersion, string kernelVersion, string operatingSystem, int numCpus, string virtualized)
            : this(operatingSystemType, kernelVersion, string.Empty, operatingSystem, string.Empty, string.Empty, string.Empty, architecture, numCpus, virtualized, string.Empty, string.Empty, version, provisioning, new Dictionary<string, string>())
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

        public string OperatingSystemVariant { get; }

        public string OperatingSystemBuild { get; }

        public string Architecture { get; }

        public int NumCpus { get; }

        public string Virtualized { get; }

        public string ProductName { get; }

        public string SystemVendor { get; }

        public string Version { get; }

        public ProvisioningInfo Provisioning { get; }

        public IDictionary<string, string> AdditionalProperties { get; }

        public string ToQueryString()
        {
            // NOTE (from author):
            // - Disgusting, but works™️
            // - Reflection will not work due to name mappings
            StringBuilder b = new StringBuilder()
                .Append($"kernel_name={UrlEncode(this.Kernel ?? string.Empty)};") 
                .Append($"kernel_release={UrlEncode(this.KernelRelease ?? string.Empty)};") 
                .Append($"kernel_version={UrlEncode(this.KernelVersion ?? string.Empty)};") 
                .Append($"os_name={UrlEncode(this.OperatingSystem ?? string.Empty)};")
                .Append($"os_version={UrlEncode(this.OperatingSystemVersion ?? string.Empty)};")
                .Append($"os_variant={UrlEncode(this.OperatingSystemVariant ?? string.Empty)};")
                .Append($"os_build_id={UrlEncode(this.OperatingSystemBuild ?? string.Empty)};")
                .Append($"cpu_architecture={UrlEncode(this.Architecture ?? string.Empty)};")
                .Append($"product_name={UrlEncode(this.ProductName ?? string.Empty)};")
                .Append($"product_vendor={UrlEncode(this.SystemVendor ?? string.Empty)};");

            if (this.AdditionalProperties != null)
            {
                foreach ((string k, string v) in this.AdditionalProperties)
                {
                    if (!string.IsNullOrEmpty(k))
                    {
                        b.Append($"{UrlEncode(k)}={UrlEncode(v ?? string.Empty)};");
                    }
                }
            }

            return b.ToString();
        }

        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty, string.Empty);
    }
}
