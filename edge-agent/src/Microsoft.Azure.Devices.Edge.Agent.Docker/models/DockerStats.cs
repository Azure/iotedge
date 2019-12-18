// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class DockerStats
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Name { get; set; }

        [JsonProperty("blkio_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Dictionary<string, DiskIO[]> BlockIoStats { get; set; }

        [JsonProperty("cpu_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public DockerCpuStats CpuStats { get; set; }

        [JsonProperty("memory_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public MemoryStats MemoryStats { get; set; }

        [JsonProperty("networks", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Dictionary<string, NetworkInfo> Networks { get; set; }

        [JsonProperty("num_procs", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int NumProcesses { get; set; }

        [JsonProperty("pids_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public PidsStats PidsStats { get; set; }

        [JsonProperty("read", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public DateTime Read { get; set; }
    }

    public class DiskIO
    {
        [JsonProperty("major", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Major { get; set; }

        [JsonProperty("minor", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Minor { get; set; }

        [JsonProperty("op", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Op { get; set; }

        [JsonProperty("value", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Value { get; set; }
    }

    public class DockerCpuStats
    {
        [JsonProperty("cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public CpuUsage CpuUsage { get; set; }

        [JsonProperty("online_cpus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int OnlineCpus { get; set; }

        [JsonProperty("system_cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong SystemCpuUsage { get; set; }
    }

    public class CpuUsage
    {
        [JsonProperty("total_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong TotalUsage { get; set; }

        [JsonProperty("usage_in_kernelmode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong UsageInKernelmode { get; set; }

        [JsonProperty("usage_in_usermode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong UsageInUsermode { get; set; }
    }

    public class MemoryStats
    {
        [JsonProperty("limit", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong Limit { get; set; }

        [JsonProperty("max_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong MaxUsage { get; set; }

        [JsonProperty("usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong Usage { get; set; }
    }

    public class NetworkInfo
    {
        [JsonProperty("rx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int RxBytes { get; set; }

        [JsonProperty("tx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int TxBytes { get; set; }
    }

    public class PidsStats
    {
        [JsonProperty("current", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int Current { get; set; }
    }
}
