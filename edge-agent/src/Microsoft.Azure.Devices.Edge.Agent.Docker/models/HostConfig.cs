// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class HostConfig
    {
        [JsonProperty("Binds", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Binds { get; set; }

        [JsonProperty("LogConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public LogConfig LogConfig { get; set; }

        [JsonProperty("Mounts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<Mount> Mounts { get; set; }

        [JsonProperty("NetworkMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string NetworkMode { get; set; }

        [JsonProperty("IpcMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string IpcMode { get; set; }

        [JsonProperty("PortBindings", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, IList<PortBinding>> PortBindings { get; set; }

        [JsonProperty("Privileged", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Privileged { get; set; }

        [JsonProperty("CpuRealtimePeriod", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPURealtimePeriod { get; set; }

        [JsonProperty("CpuQuota", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPUQuota { get; set; }

        [JsonProperty("CpuPeriod", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPUPeriod { get; set; }

        [JsonProperty("BlkioDeviceWriteIOps", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<ThrottleDevice> BlkioDeviceWriteIOps { get; set; }

        [JsonProperty("BlkioDeviceReadIOps", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<ThrottleDevice> BlkioDeviceReadIOps { get; set; }

        [JsonProperty("BlkioDeviceWriteBps", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<ThrottleDevice> BlkioDeviceWriteBps { get; set; }

        [JsonProperty("BlkioDeviceReadBps", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<ThrottleDevice> BlkioDeviceReadBps { get; set; }

        [JsonProperty("BlkioWeightDevice", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<WeightDevice> BlkioWeightDevice { get; set; }

        [JsonProperty("BlkioWeight", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ushort BlkioWeight { get; set; }

        [JsonProperty("CgroupParent", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string CgroupParent { get; set; }

        [JsonProperty("NanoCpus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long NanoCPUs { get; set; }

        [JsonProperty("Memory", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Memory { get; set; }

        [JsonProperty("CpuShares", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPUShares { get; set; }

        [JsonProperty("CpuRealtimeRuntime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPURealtimeRuntime { get; set; }

        [JsonProperty("CpusetCpus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string CpusetCpus { get; set; }

        [JsonProperty("CpusetMems", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string CpusetMems { get; set; }

        [JsonProperty("Devices", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<DeviceMapping> Devices { get; set; }

        [JsonProperty("DiskQuota", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long DiskQuota { get; set; }

        [JsonProperty("KernelMemory", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long KernelMemory { get; set; }

        [JsonProperty("MemoryReservation", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long MemoryReservation { get; set; }

        [JsonProperty("MemorySwap", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long MemorySwap { get; set; }

        [JsonProperty("MemorySwappiness", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long? MemorySwappiness { get; set; }

        [JsonProperty("OomKillDisable", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool? OomKillDisable { get; set; }

        [JsonProperty("PidsLimit", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long? PidsLimit { get; set; }

        [JsonProperty("Ulimits", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<Ulimit> Ulimits { get; set; }

        [JsonProperty("CpuCount", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPUCount { get; set; }

        [JsonProperty("CpuPercent", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long CPUPercent { get; set; }

        [JsonProperty("IOMaximumIOps", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong IOMaximumIOps { get; set; }

        [JsonProperty("IOMaximumBandwidth", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong IOMaximumBandwidth { get; set; }

        [JsonProperty("Isolation", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Isolation { get; set; }

        [JsonProperty("ConsoleSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong[] ConsoleSize { get; set; }

        [JsonProperty("Runtime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Runtime { get; set; }

        [JsonProperty("Sysctls", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> Sysctls { get; set; }

        [JsonProperty("ContainerIDFile", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string ContainerIDFile { get; set; }

        [JsonProperty("RestartPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public RestartPolicy RestartPolicy { get; set; }

        [JsonProperty("AutoRemove", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool AutoRemove { get; set; }

        [JsonProperty("VolumeDriver", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string VolumeDriver { get; set; }

        [JsonProperty("VolumesFrom", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> VolumesFrom { get; set; }

        [JsonProperty("CapAdd", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> CapAdd { get; set; }

        [JsonProperty("CapDrop", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> CapDrop { get; set; }

        [JsonProperty("Dns", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> DNS { get; set; }

        [JsonProperty("DnsOptions", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> DNSOptions { get; set; }

        [JsonProperty("DnsSearch", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> DNSSearch { get; set; }

        [JsonProperty("Init", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool? Init { get; set; }

        [JsonProperty("ExtraHosts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> ExtraHosts { get; set; }

        [JsonProperty("Cgroup", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Cgroup { get; set; }

        [JsonProperty("Links", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Links { get; set; }

        [JsonProperty("OomScoreAdj", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long OomScoreAdj { get; set; }

        [JsonProperty("PidMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string PidMode { get; set; }

        [JsonProperty("PublishAllPorts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool PublishAllPorts { get; set; }

        [JsonProperty("ReadonlyRootfs", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ReadonlyRootfs { get; set; }

        [JsonProperty("SecurityOpt", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> SecurityOpt { get; set; }

        [JsonProperty("StorageOpt", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> StorageOpt { get; set; }

        [JsonProperty("Tmpfs", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> Tmpfs { get; set; }

        [JsonProperty("UTSMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string UTSMode { get; set; }

        [JsonProperty("UsernsMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string UsernsMode { get; set; }

        [JsonProperty("ShmSize", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long ShmSize { get; set; }

        [JsonProperty("GroupAdd", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> GroupAdd { get; set; }

        [JsonProperty("InitPath", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string InitPath { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
