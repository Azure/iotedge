// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;

#pragma warning disable SA1300 // Element should begin with upper-case letter
    public class DockerStats
    {
        public string name { get; set; }
        public Dictionary<string, DiskIO[]> blkio_stats { get; set; }
        public DockerCpuStats cpu_stats { get; set; }
        public MemoryStats memory_stats { get; set; }
        public Dictionary<string, NetworkInfo> networks { get; set; }
        public int num_procs { get; set; }
        public PidsStats pids_stats { get; set; }
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
