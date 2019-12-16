// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class DockerStats
    {
        [JsonConstructor]
        public DockerStats(string name, Dictionary<string, DiskIO[]> block_io_stats, DockerCpuStats cpu_stats, MemoryStats memory_stats, Dictionary<string, NetworkInfo> networks, int? num_processes, PidsStats pids_stats, DateTime? read)
        {
            this.Name = name ?? "error_name";
            this.BlockIoStats = block_io_stats ?? new Dictionary<string, DiskIO[]>();
            this.CpuStats = cpu_stats ?? new DockerCpuStats(null, null, null);
            this.MemoryStats = memory_stats ?? new MemoryStats(null, null, null);
            this.Networks = networks ?? new Dictionary<string, NetworkInfo>();
            this.NumProcesses = num_processes ?? 0;
            this.PidsStats = pids_stats ?? new PidsStats(null);
            this.Read = read ?? DateTime.MinValue;
        }

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
        [JsonConstructor]
        public DiskIO(long? major, long? minor, string op, long? value)
        {
            this.Major = major ?? 0;
            this.Minor = minor ?? 0;
            this.Op = op ?? "Error";
            this.Value = value ?? 0;
        }

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
        [JsonConstructor]
        public DockerCpuStats(CpuUsage cpu_usage, int? online_cpus, ulong? system_cpu_usage)
        {
            this.CpuUsage = cpu_usage ?? new CpuUsage(null, null, null);
            this.OnlineCpus = online_cpus ?? 0;
            this.SystemCpuUsage = system_cpu_usage ?? 0;
        }

        [JsonProperty("cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public CpuUsage CpuUsage { get; set; }

        [JsonProperty("online_cpus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int OnlineCpus { get; set; }

        [JsonProperty("system_cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong SystemCpuUsage { get; set; }
    }

    public class CpuUsage
    {
        [JsonConstructor]
        public CpuUsage(ulong? total_usage, ulong? usage_in_kernelmode, ulong? usage_in_usermode)
        {
            this.TotalUsage = total_usage ?? 0;
            this.UsageInKernelmode = usage_in_kernelmode ?? 0;
            this.UsageInUsermode = usage_in_usermode ?? 0;
        }

        [JsonProperty("total_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong TotalUsage { get; set; }

        [JsonProperty("usage_in_kernelmode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong UsageInKernelmode { get; set; }

        [JsonProperty("usage_in_usermode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong UsageInUsermode { get; set; }
    }

    public class MemoryStats
    {
        [JsonConstructor]
        public MemoryStats(ulong? limit, ulong? max_usage, ulong? usage)
        {
            this.Limit = limit ?? 0;
            this.MaxUsage = max_usage ?? 0;
            this.Usage = usage ?? 0;
        }

        [JsonProperty("limit", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong Limit { get; set; }

        [JsonProperty("max_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong MaxUsage { get; set; }

        [JsonProperty("usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong Usage { get; set; }
    }

    public class NetworkInfo
    {
        [JsonConstructor]
        public NetworkInfo(int? rx_bytes, int? tx_bytes)
        {
            this.RxBytes = rx_bytes ?? 0;
            this.TxBytes = tx_bytes ?? 0;
        }

        [JsonProperty("rx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int RxBytes { get; set; }

        [JsonProperty("tx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int TxBytes { get; set; }
    }

    public class PidsStats
    {
        [JsonConstructor]
        public PidsStats(int? current)
        {
            this.Current = current ?? 0;
        }

        [JsonProperty("current", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int Current { get; set; }
    }
}
