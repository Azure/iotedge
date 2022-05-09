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
        public SystemInfo(string operatingSystemType, string architecture, string version, ProvisioningInfo provisioning, string serverVersion, string kernelVersion, string operatingSystem, int numCpus, string virtualized, IDictionary<string, object> additionalProperties)
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
            this.AdditionalProperties = additionalProperties;
        }

        public SystemInfo(string operatingSystemType, string architecture, string version, IDictionary<string, object> additionalProperties)
            : this(operatingSystemType, architecture, version, ProvisioningInfo.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, additionalProperties)
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

        public string Virtualized { get; }

        // NOTE: changed to IDictionary from IReadOnlyDictionary since the
        // latter cannot be used as extension data.  Likewise for <string,
        // object> from <string, string>.
        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties { get; }

        public string ToQueryString()
        {
            StringBuilder b = new StringBuilder();

            foreach (PropertyInfo property in this.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(string) || property.PropertyType == typeof(int))
                {
                    b.Append($"{property.Name}={UrlEncode(property.GetValue(this)?.ToString() ?? string.Empty)};");
                }
            }

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
