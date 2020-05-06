// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SystemResourcesMetrics : ISystemResourcesMetrics, IDisposable
    {
        public static readonly TimeSpan MaxUpdateFrequency = TimeSpan.FromSeconds(5);
        static readonly string[] EdgeRuntimeModules = { Constants.EdgeAgentModuleName, Constants.EdgeHubModuleName };

        Func<Task<SystemResources>> getSystemResources;
        PeriodicTask updateResources;
        string apiVersion;
        TimeSpan updateFrequency;

        IMetricsGauge hostUptime;
        IMetricsGauge iotedgedUptime;
        IMetricsGauge usedSpace;
        IMetricsGauge totalSpace;
        IMetricsGauge usedMemory;
        IMetricsGauge totalMemory;
        IMetricsHistogram cpuPercentage;
        IMetricsGauge createdPids;
        IMetricsGauge networkIn;
        IMetricsGauge networkOut;
        IMetricsGauge diskRead;
        IMetricsGauge diskWrite;

        // Used to calculate cpu percentage
        Dictionary<string, double> previousModuleCpu = new Dictionary<string, double>();
        Dictionary<string, double> previousSystemCpu = new Dictionary<string, double>();
        Dictionary<string, DateTime> previousReadTime = new Dictionary<string, DateTime>();

        public SystemResourcesMetrics(IMetricsProvider metricsProvider, Func<Task<SystemResources>> getSystemResources, string apiVersion, TimeSpan updateFrequency)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.getSystemResources = Preconditions.CheckNotNull(getSystemResources, nameof(getSystemResources));
            this.apiVersion = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));
            Preconditions.CheckArgument(updateFrequency >= MaxUpdateFrequency, $"Performance metrics cannot update faster than {MaxUpdateFrequency.Humanize()}.");
            this.updateFrequency = updateFrequency;

            this.hostUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "host_uptime_seconds",
                "How long the host has been on",
                new List<string> { MetricsConstants.MsTelemetry }));

            this.iotedgedUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "iotedged_uptime_seconds",
                "How long iotedged has been running",
                new List<string> { MetricsConstants.MsTelemetry }));

            this.usedSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "available_disk_space_bytes",
                "Amount of space left on the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype", MetricsConstants.MsTelemetry }));

            this.totalSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_space_bytes",
                "Size of the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype", MetricsConstants.MsTelemetry }));

            this.usedMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "used_memory_bytes",
                "Amount of RAM used by all processes",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.totalMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_memory_bytes",
                "RAM available",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.cpuPercentage = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
                "used_cpu_percent",
                "Percent of cpu used by all processes",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.createdPids = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "created_pids_total",
                "The number of processes or threads the container has created",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.networkIn = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_network_in_bytes",
                "The amount of bytes recieved from the network",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.networkOut = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_network_out_bytes",
                "The amount of bytes sent to network",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.diskRead = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_read_bytes",
                "The amount of bytes read from the disk",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));

            this.diskWrite = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_write_bytes",
                "The amount of bytes written to disk",
                new List<string> { "module_name", MetricsConstants.MsTelemetry }));
        }

        public void Start(ILogger logger)
        {
            if (ApiVersion.ParseVersion(this.apiVersion).Value >= ApiVersion.Version20191105.Value)
            {
                logger.LogInformation($"Updating performance metrics every {this.updateFrequency.Humanize()}");
                this.updateResources = new PeriodicTask(this.Update, this.updateFrequency, TimeSpan.FromMinutes(1), logger, "Get system resources", false);
            }
            else
            {
                logger.LogInformation($"Skipping host metrics. Management api version too low: {this.apiVersion}");
            }
        }

        public void Dispose()
        {
            this.updateResources?.Dispose();
        }

        async Task Update()
        {
            SystemResources systemResources = await this.getSystemResources();

            var uptimeTags = new string[] { true.ToString() };
            this.hostUptime.Set(systemResources.HostUptime, uptimeTags);
            this.iotedgedUptime.Set(systemResources.IotEdgedUptime, uptimeTags);

            var hostTags = new string[] { "host", true.ToString() };
            // edgelet sets used cpu to -1 on error
            if (systemResources.UsedCpu > 0)
            {
                this.cpuPercentage.Update(systemResources.UsedCpu, hostTags);
            }

            this.usedMemory.Set(systemResources.UsedRam, hostTags);
            this.totalMemory.Set(systemResources.TotalRam, hostTags);

            foreach (Disk disk in systemResources.Disks)
            {
                var diskTags = new string[] { disk.Name, disk.FileSystem, disk.FileType, true.ToString() };
                this.usedSpace.Set(disk.AvailableSpace, diskTags);
                this.totalSpace.Set(disk.TotalSpace, diskTags);
            }

            this.SetModuleStats(systemResources);
        }

        void SetModuleStats(SystemResources systemResources)
        {
            DockerStats[] modules = JsonConvert.DeserializeObject<DockerStats[]>(systemResources.ModuleStats);
            foreach (DockerStats module in modules)
            {
                if (!module.Name.HasValue)
                {
                    continue;
                }

                string name = module.Name.OrDefault().Substring(1); // remove '/' from start of name
                var tags = new string[] { name, EdgeRuntimeModules.Contains(name).ToString() };

                this.GetCpuUsage(module, name).ForEach(usedCpu => this.cpuPercentage.Update(usedCpu, tags));
                module.MemoryStats.ForEach(ms =>
                {
                    ms.Limit.ForEach(limit => this.totalMemory.Set(limit, tags));
                    ms.Usage.ForEach(usage => this.usedMemory.Set((long)usage, tags));
                });
                module.PidsStats.ForEach(ps => ps.Current.ForEach(current => this.createdPids.Set(current, tags)));

                module.Networks.ForEach(network =>
                {
                    this.networkIn.Set(network.Sum(n => n.Value.RxBytes.OrDefault()), tags);
                    this.networkOut.Set(network.Sum(n => n.Value.TxBytes.OrDefault()), tags);
                });

                module.BlockIoStats.ForEach(disk =>
                {
                    this.diskRead.Set(disk.Sum(io => io.Value.Where(d => d.Op.Exists(op => op == "Read")).Sum(d => d.Value.OrDefault())), tags);
                    this.diskWrite.Set(disk.Sum(io => io.Value.Where(d => d.Op.Exists(op => op == "Write")).Sum(d => d.Value.OrDefault())), tags);
                });
            }
        }

        Option<double> GetCpuUsage(DockerStats module, string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Get values if exist
                double totalUsage = 0, systemUsage = 0;
                if (!module.CpuStats.Exists(cpuStats => cpuStats.CpuUsage.Exists(cpuUsage => cpuUsage.TotalUsage.Exists(tu =>
                     {
                         totalUsage = tu;
                         return true;
                     })) && cpuStats.SystemCpuUsage.Exists(su =>
                     {
                         systemUsage = su;
                         return true;
                     })))
                {
                    // One of the values is missing, skip.
                    return Option.None<double>();
                }

                // Calculate
                if (this.previousModuleCpu.TryGetValue(name, out double prevModule) && this.previousSystemCpu.TryGetValue(name, out double prevSystem))
                {
                    double moduleDiff = totalUsage - prevModule;
                    double systemDiff = systemUsage - prevSystem;
                    if (systemDiff > 0)
                    {
                        double result = 100 * moduleDiff / systemDiff;

                        // Occasionally on startup results in a very large number (billions of percent). Ignore this point.
                        if (result < 100)
                        {
                            return Option.Some(result);
                        }
                    }
                }

                this.previousModuleCpu[name] = totalUsage;
                this.previousSystemCpu[name] = systemUsage;
            }
            else
            {
                // Get values if exist
                double totalUsage = 0;
                DateTime readTime = DateTime.MinValue;
                if (!(module.CpuStats.Exists(cpuStats => cpuStats.CpuUsage.Exists(cpuUsage => cpuUsage.TotalUsage.Exists(tu =>
                    {
                        totalUsage = tu;
                        return true;
                    }))) && module.Read.Exists(read =>
                    {
                        readTime = read;
                        return true;
                    })))
                {
                    // One of the values is missing, skip.
                    return Option.None<double>();
                }

                // Calculate
                if (this.previousModuleCpu.TryGetValue(name, out double prevModule) && this.previousReadTime.TryGetValue(name, out DateTime prevTime))
                {
                    double totalIntervals = (readTime - prevTime).TotalMilliseconds * 10; // Get number of 100ns intervals during read
                    double intervalsUsed = totalUsage - prevModule;

                    if (totalIntervals > 0)
                    {
                        return Option.Some(100 * intervalsUsed / totalIntervals);
                    }
                }

                this.previousModuleCpu[name] = totalUsage;
                this.previousReadTime[name] = readTime;
            }

            return Option.None<double>();
        }
    }
}
