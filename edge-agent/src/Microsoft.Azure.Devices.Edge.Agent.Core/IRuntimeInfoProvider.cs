// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This interface provides the module runtime information.
    /// TODO: Consider replacing this with IEnvironment and the decorator pattern.
    /// However, that would require IModule implementations to be made generic.
    /// </summary>
    public interface IRuntimeInfoProvider
    {
        Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken);

        Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, CancellationToken cancellationToken);

        Task<SystemInfo> GetSystemInfo();
    }

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
