// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Newtonsoft.Json;

    public class SystemInfo
    {
        [JsonConstructor]
        public SystemInfo(string operatingSystemType, string architecture, string version)
        {
            this.OperatingSystemType = operatingSystemType;
            this.Architecture = architecture;
            this.Version = version;
        }

        public string OperatingSystemType { get; }

        public string Architecture { get; }

        public string Version { get; }

        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty, string.Empty);
    }
}
