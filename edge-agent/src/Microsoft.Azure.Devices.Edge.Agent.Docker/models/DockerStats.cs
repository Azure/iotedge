// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This class is just used to parse the docker stats api.
    /// </summary>
    public class DockerStats
    {
        [JsonConstructor]
        public DockerStats(string name, Dictionary<string, DiskIO[]> blkio_stats, DockerCpuStats cpu_stats, DockerCpuStats precpu_stats, MemoryStats memory_stats, Dictionary<string, NetworkInfo> networks, int? num_procs, PidsStats pids_stats, DateTime? read, DateTime? preread)
        {
            this.Name = Option.Maybe(name);
            this.BlockIoStats = Option.Maybe(blkio_stats);
            this.CpuStats = Option.Maybe(cpu_stats);
            this.PreviousCpuStats = Option.Maybe(precpu_stats);
            this.MemoryStats = Option.Maybe(memory_stats);
            this.Networks = Option.Maybe(networks);
            this.NumProcesses = Option.Maybe(num_procs);
            this.PidsStats = Option.Maybe(pids_stats);
            this.Read = Option.Maybe(read);
            this.PreviousRead = Option.Maybe(preread);

            // Remove null entries in dictionarys
            this.BlockIoStats.ForEach(stats => stats.Where(stat => stat.Value == null).ToList().ForEach(stat => stats.Remove(stat.Key)));
            this.Networks.ForEach(network => network.Where(n => n.Value == null).ToList().ForEach(n => network.Remove(n.Key)));
        }

        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<string> Name { get; }

        [JsonProperty("blkio_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<Dictionary<string, DiskIO[]>> BlockIoStats { get; }

        [JsonProperty("cpu_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<DockerCpuStats> CpuStats { get; }

        [JsonProperty("precpu_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<DockerCpuStats> PreviousCpuStats { get; }

        [JsonProperty("memory_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<MemoryStats> MemoryStats { get; }

        [JsonProperty("networks", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<Dictionary<string, NetworkInfo>> Networks { get; }

        [JsonProperty("num_procs", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<int> NumProcesses { get; }

        [JsonProperty("pids_stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<PidsStats> PidsStats { get; }

        [JsonProperty("read", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<DateTime> Read { get; }

        [JsonProperty("preread", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<DateTime> PreviousRead { get; }
    }

    public class DiskIO
    {
        [JsonConstructor]
        public DiskIO(double? major, double? minor, string op, double? value)
        {
            this.Major = Option.Maybe(major);
            this.Minor = Option.Maybe(minor);
            this.Op = Option.Maybe(op);
            this.Value = Option.Maybe(value);
        }

        [JsonProperty("major", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Major { get; }

        [JsonProperty("minor", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Minor { get; }

        [JsonProperty("op", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<string> Op { get; }

        [JsonProperty("value", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Value { get; }
    }

    public class DockerCpuStats
    {
        [JsonConstructor]
        public DockerCpuStats(CpuUsage cpu_usage, int? online_cpus, double? system_cpu_usage)
        {
            this.CpuUsage = Option.Maybe(cpu_usage);
            this.OnlineCpus = Option.Maybe(online_cpus);
            this.SystemCpuUsage = Option.Maybe(system_cpu_usage);
        }

        [JsonProperty("cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<CpuUsage> CpuUsage { get; }

        [JsonProperty("online_cpus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<int> OnlineCpus { get; }

        [JsonProperty("system_cpu_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> SystemCpuUsage { get; }
    }

    public class CpuUsage
    {
        [JsonConstructor]
        public CpuUsage(double? total_usage, double? usage_in_kernelmode, double? usage_in_usermode)
        {
            this.TotalUsage = Option.Maybe(total_usage);
            this.UsageInKernelmode = Option.Maybe(usage_in_kernelmode);
            this.UsageInUsermode = Option.Maybe(usage_in_usermode);
        }

        [JsonProperty("total_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> TotalUsage { get; }

        [JsonProperty("usage_in_kernelmode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> UsageInKernelmode { get; }

        [JsonProperty("usage_in_usermode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> UsageInUsermode { get; }
    }

    public class MemoryStats
    {
        [JsonConstructor]
        public MemoryStats(double? privateworkingset, double? limit, double? max_usage, double? usage, MemoryStatsExtended stats)
        {
            this.PrivateWorkingSet = Option.Maybe(privateworkingset);
            this.Limit = Option.Maybe(limit);
            this.MaxUsage = Option.Maybe(max_usage);
            this.Usage = Option.Maybe(usage);
            this.Stats = Option.Maybe(stats);
        }

        [JsonProperty("privateworkingset", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> PrivateWorkingSet { get; }

        [JsonProperty("limit", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Limit { get; }

        [JsonProperty("max_usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> MaxUsage { get; }

        [JsonProperty("usage", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Usage { get; }

        [JsonProperty("stats", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<MemoryStatsExtended> Stats { get; }
    }

    public class MemoryStatsExtended
    {
        [JsonConstructor]
        public MemoryStatsExtended(double? cache)
        {
            this.Cache = Option.Maybe(cache);
        }

        [JsonProperty("cache", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Cache { get; }
    }

    public class NetworkInfo
    {
        [JsonConstructor]
        public NetworkInfo(double? rx_bytes, double? tx_bytes)
        {
            this.RxBytes = Option.Maybe(rx_bytes);
            this.TxBytes = Option.Maybe(tx_bytes);
        }

        [JsonProperty("rx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> RxBytes { get; }

        [JsonProperty("tx_bytes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> TxBytes { get; }
    }

    public class PidsStats
    {
        [JsonConstructor]
        public PidsStats(double? current)
        {
            this.Current = Option.Maybe(current);
        }

        [JsonProperty("current", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<double> Current { get; }
    }
}
