// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    using static System.Net.WebUtility;

    public class SystemInfo
    {
        public SystemInfo(string operatingSystemType, string architecture, string version, ProvisioningInfo provisioning, string serverVersion, string kernelVersion, string operatingSystem, int numCpus, long totalMemory, string virtualized, IReadOnlyDictionary<string, object> additionalProperties)
        {
            this.OperatingSystemType = operatingSystemType;
            this.Architecture = architecture;
            this.Version = version;
            this.Provisioning = provisioning;
            this.ServerVersion = serverVersion;
            this.KernelVersion = kernelVersion;
            this.OperatingSystem = operatingSystem;
            this.NumCpus = numCpus;
            this.TotalMemory = totalMemory;
            this.Virtualized = virtualized;
            this.AdditionalProperties = additionalProperties;
        }

        public SystemInfo(string operatingSystemType, string architecture, string version, IReadOnlyDictionary<string, object> additionalProperties)
            : this(operatingSystemType, architecture, version, ProvisioningInfo.Empty, string.Empty, string.Empty, string.Empty, 0, 0, string.Empty, additionalProperties)
        {
        }

        public SystemInfo(string operatingSystemType, string architecture, string version)
            : this(operatingSystemType, architecture, version, new Dictionary<string, object>())
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

        public long TotalMemory { get; }

        public string Virtualized { get; }

        [Newtonsoft.Json.JsonIgnore]
        public IReadOnlyDictionary<string, object> AdditionalProperties { get; }

        public string ToQueryString()
        {
            StringBuilder b = new StringBuilder()
                .Append($"kernel={UrlEncode(this.OperatingSystemType ?? string.Empty)};")
                .Append($"architecture={UrlEncode(this.Architecture ?? string.Empty)};")
                .Append($"version={UrlEncode(this.Version ?? string.Empty)};")
                .Append($"server_version={UrlEncode(this.ServerVersion ?? string.Empty)};")
                .Append($"kernel_version={UrlEncode(this.KernelVersion ?? string.Empty)};")
                .Append($"operating_system={UrlEncode(this.OperatingSystem ?? string.Empty)};")
                .Append($"cpus={this.NumCpus};")
                .Append($"total_memory={this.TotalMemory};")
                .Append($"virtualized={UrlEncode(this.Virtualized ?? string.Empty)};");

            if (this.AdditionalProperties != null)
            {
                foreach ((string k, object v) in this.AdditionalProperties)
                {
                    if (!string.IsNullOrEmpty(k))
                    {
                        b.Append($"{UrlEncode(k)}={UrlEncode(v?.ToString() ?? string.Empty)};");
                    }
                }
            }

            return b.ToString();
        }

        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty, string.Empty);
    }
}
