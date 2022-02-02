// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using static System.Net.WebUtility;

    public class SystemInfo
    {
        public SystemInfo(string operatingSystemType, string architecture, string version, IDictionary<string, object> additionalProperties)
        {
            this.OperatingSystemType = operatingSystemType;
            this.Architecture = architecture;
            this.Version = version;

            this.AdditionalProperties = additionalProperties?.ToDictionary(entry => entry.Key, entry => entry.Value?.ToString());
        }

        public SystemInfo(string operatingSystemType, string architecture, string version)
            : this(operatingSystemType, architecture, version, new Dictionary<string, object>())
        {
        }

        public string OperatingSystemType { get; }

        public string Architecture { get; }

        public string Version { get; }

        public IDictionary<string, string> AdditionalProperties { get; }

        public string ToQueryString()
        {
            // NOTE (from author): Reflection will not work due to name mappings
            StringBuilder b = new StringBuilder()
                .Append($"kernel_name={UrlEncode(this.OperatingSystemType ?? string.Empty)};")
                .Append($"cpu_architecture={UrlEncode(this.Architecture ?? string.Empty)};");

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
