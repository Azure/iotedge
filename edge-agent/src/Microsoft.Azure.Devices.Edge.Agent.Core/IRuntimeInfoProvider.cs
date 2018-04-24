// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// This interface provides the module runtime information.
    /// TODO: Consider replacing this with IEnvironment and the decorator pattern.
    /// However, that would require IModule implementations to be made generic. 
    /// </summary>
    public interface IRuntimeInfoProvider
    {
        Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken ctsToken);

        Task<SystemInfo> GetSystemInfo();
    }

    public class SystemInfo
    {
        static SystemInfo Empty { get; } = new SystemInfo(string.Empty, string.Empty);

        [JsonConstructor]
        public SystemInfo(string operatingSystemType, string architecture)
        {
            this.OperatingSystemType = operatingSystemType;
            this.Architecture = architecture;
        }

        public string OperatingSystemType { get; }

        public string Architecture { get; }
    }
}
