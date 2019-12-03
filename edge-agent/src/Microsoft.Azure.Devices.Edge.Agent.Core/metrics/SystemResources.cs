// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SystemResources
    {
        public SystemResources(long hostUptime, long iotEdgedUptime, double usedCpu, long usedRam, long totalRam, Disk[] disks, string dockerStats)
        {
            this.HostUptime = Preconditions.CheckNotNull(hostUptime, nameof(hostUptime));
            this.IotEdgedUptime = Preconditions.CheckNotNull(iotEdgedUptime, nameof(iotEdgedUptime));
            this.UsedCpu = Preconditions.CheckNotNull(usedCpu, nameof(usedCpu));
            this.UsedRam = Preconditions.CheckNotNull(usedRam, nameof(usedRam));
            this.TotalRam = Preconditions.CheckNotNull(totalRam, nameof(totalRam));
            this.Disks = Preconditions.CheckNotNull(disks, nameof(disks));
            this.ModuleStats = JsonConvert.DeserializeObject<ModuleStats[]>(Preconditions.CheckNotNull(dockerStats, nameof(dockerStats)));
        }

        public long HostUptime { get; }

        public long IotEdgedUptime { get; }

        public double UsedCpu { get; }

        public long UsedRam { get; }

        public long TotalRam { get; }

        public Disk[] Disks { get; }

        public ModuleStats[] ModuleStats { get; }
    }

    public class Disk
    {
        public Disk(string name, long availableSpace, long totalSpace, string fileSystem, string fileType)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.AvailableSpace = Preconditions.CheckNotNull(availableSpace, nameof(availableSpace));
            this.TotalSpace = Preconditions.CheckNotNull(totalSpace, nameof(totalSpace));
            this.FileSystem = Preconditions.CheckNotNull(fileSystem, nameof(fileSystem));
            this.FileType = Preconditions.CheckNotNull(fileType, nameof(fileType));
        }

        public string Name { get; }

        public long AvailableSpace { get; }

        public long TotalSpace { get; }

        public string FileSystem { get; }

        public string FileType { get; }
    }

#pragma warning disable SA1300 // Element should begin with upper-case letter

    public class ModuleStats
    {
        // TODO: change to moduleName
        public string module { get; set; }
        public DockerStats stats { get; set; }
    }

    public class DockerStats
    {
        public Dictionary<string, DiskIO[]> blkio_stats { get; set; }
        public DockerCpuStats cpu_stats { get; set; }
        public DockerCpuStats precpu_stats { get; set; }
        public MemoryStats memory_stats { get; set; }
        public Dictionary<string, NetworkInfo> networks { get; set; }
        public int num_procs { get; set; }
        public PidsStats pids_stats { get; set; }
        public DateTime preread { get; set; }
        public DateTime read { get; set; }
    }

    public class DiskIO
    {
        public long major { get; set; }
        public long minor { get; set; }
        public string op { get; set; }
        public long value { get; set; }
    }

    public class DockerCpuStats
    {
        public CpuUsage cpu_usage { get; set; }
        public int online_cpus { get; set; }
        public ulong system_cpu_usage { get; set; }
    }

    public class CpuUsage
    {
        public ulong total_usage { get; set; }
        public ulong usage_in_kernelmode { get; set; }
        public ulong usage_in_usermode { get; set; }
    }

    public class MemoryStats
    {
        public ulong limit { get; set; }
        public ulong max_usage { get; set; }
        public ulong usage { get; set; }
    }

    public class NetworkInfo
    {
        public int rx_bytes { get; set; }
        public int rx_dropped { get; set; }
        public int rx_errors { get; set; }
        public int rx_packets { get; set; }
        public int tx_bytes { get; set; }
        public int tx_dropped { get; set; }
        public int tx_errors { get; set; }
        public int tx_packets { get; set; }
    }

    public class PidsStats
    {
        public int current { get; set; }
    }
#pragma warning restore SA1300 // Element should begin with upper-case letter
}
