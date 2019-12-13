// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SystemResourcesMetrics : IDisposable
    {
        Func<Task<SystemResources>> getSystemResources;
        PeriodicTask updateResources;
        string apiVersion;

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
        Dictionary<string, ulong> previousModuleCpu = new Dictionary<string, ulong>();
        Dictionary<string, ulong> previousSystemCpu = new Dictionary<string, ulong>();
        Dictionary<string, DateTime> previousReadTime = new Dictionary<string, DateTime>();

        public SystemResourcesMetrics(IMetricsProvider metricsProvider, Func<Task<SystemResources>> getSystemResources, string apiVersion)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.getSystemResources = Preconditions.CheckNotNull(getSystemResources, nameof(getSystemResources));
            this.apiVersion = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));

            this.hostUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "host_uptime_seconds",
                "How long the host has been on",
                new List<string> { }));

            this.iotedgedUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "iotedged_uptime_seconds",
                "How long iotedged has been running",
                new List<string> { }));

            this.usedSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "available_disk_space_bytes",
                "Amount of space left on the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.totalSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_space_bytes",
                "Size of the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.usedMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "used_memory_bytes",
                "Amount of RAM used by all processes",
                new List<string> { "module" }));

            this.totalMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_memory_bytes",
                "RAM available",
                new List<string> { "module" }));

            this.cpuPercentage = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
                "used_cpu_percent",
                "Percent of cpu used by all processes",
                new List<string> { "module" }));

            this.createdPids = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "created_pids_total",
                "The number of processes or threads the container has created",
                new List<string> { "module" }));

            this.networkIn = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_network_in_bytes",
                "The amount of bytes recieved from the network",
                new List<string> { "module" }));

            this.networkOut = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_network_out_bytes",
                "The amount of bytes sent to network",
                new List<string> { "module" }));

            this.diskRead = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_read_bytes",
                "The amount of bytes read from the disk",
                new List<string> { "module" }));

            this.diskWrite = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_write_bytes",
                "The amount of bytes written to disk",
                new List<string> { "module" }));
        }

        public void Start(ILogger logger)
        {
            if (ApiVersion.ParseVersion(this.apiVersion).Value >= ApiVersion.Version20191105.Value)
            {
                this.updateResources = new PeriodicTask(this.Update, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), logger, "Get system resources", false);
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

            this.hostUptime.Set(systemResources.HostUptime, new string[] { });
            this.iotedgedUptime.Set(systemResources.IotEdgedUptime, new string[] { });

            var hostTags = new string[] { "host" };
            // edgelet sets used cpu to -1 on error
            if (systemResources.UsedCpu > 0)
            {
                this.cpuPercentage.Update(systemResources.UsedCpu, hostTags);
            }

            this.usedMemory.Set(systemResources.UsedRam, hostTags);
            this.totalMemory.Set(systemResources.TotalRam, hostTags);

            foreach (Disk disk in systemResources.Disks)
            {
                this.usedSpace.Set(disk.AvailableSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
                this.totalSpace.Set(disk.TotalSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
            }

            this.SetModuleStats(systemResources);
        }

        void SetModuleStats(SystemResources systemResources)
        {
            DockerStats[] modules = JsonConvert.DeserializeObject<DockerStats[]>(systemResources.ModuleStats);
            foreach (DockerStats module in modules)
            {
                string name = module.Name.Substring(1); // remove '/' from start of name
                var tags = new string[] { name };

                this.cpuPercentage.Update(this.GetCpuUsage(module), tags);
                this.totalMemory.Set(module.MemoryStats.Limit, tags);
                this.usedMemory.Set((long)module.MemoryStats.Usage, tags);
                this.createdPids.Set(module.PidsStats.Current, tags);

                this.networkIn.Set(module.Networks.Sum(n => n.Value.RxBytes), tags);
                this.networkOut.Set(module.Networks.Sum(n => n.Value.TxBytes), tags);
                this.diskRead.Set(module.BlockIoStats.Sum(io => io.Value.Where(d => d.Op == "Read").Sum(d => d.Value)), tags);
                this.diskWrite.Set(module.BlockIoStats.Sum(io => io.Value.Where(d => d.Op == "Write").Sum(d => d.Value)), tags);
            }
        }

        double GetCpuUsage(DockerStats module)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                double result = 0;
                if (this.previousModuleCpu.TryGetValue(module.Name, out ulong prevModule) && this.previousSystemCpu.TryGetValue(module.Name, out ulong prevSystem))
                {
                    double moduleDiff = module.CpuStats.CpuUsage.TotalUsage - prevModule;
                    double systemDiff = module.CpuStats.SystemCpuUsage - prevSystem;
                    result = moduleDiff / systemDiff;
                }

                this.previousModuleCpu[module.Name] = module.CpuStats.CpuUsage.TotalUsage;
                this.previousSystemCpu[module.Name] = module.CpuStats.SystemCpuUsage;

                return result;
            }
            else
            {
                double result = 0;
                if (this.previousModuleCpu.TryGetValue(module.Name, out ulong prevModule) && this.previousReadTime.TryGetValue(module.Name, out DateTime prevTime))
                {
                    double totalIntervals = (module.Read - prevTime).TotalMilliseconds * 10; // Get number of 100ns intervals during read
                    ulong intervalsUsed = module.CpuStats.CpuUsage.TotalUsage - prevModule;

                    if (totalIntervals > 0)
                    {
                        result = intervalsUsed / totalIntervals;
                    }
                }

                this.previousModuleCpu[module.Name] = module.CpuStats.CpuUsage.TotalUsage;
                this.previousReadTime[module.Name] = module.Read;

                return result;
            }
        }
    }
}
