// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
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
        IMetricsHistogram usedSpace;
        IMetricsGauge totalSpace;
        IMetricsHistogram usedMemory;
        IMetricsGauge totalMemory;
        IMetricsHistogram cpuPercentage;
        IMetricsGauge createdPids;

        public SystemResourcesMetrics(IMetricsProvider metricsProvider, Func<Task<SystemResources>> getSystemResources, string apiVersion)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.getSystemResources = Preconditions.CheckNotNull(getSystemResources, nameof(getSystemResources));
            this.apiVersion = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));

            this.hostUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "host_uptime",
                "How long the host has been on",
                new List<string> { }));

            this.iotedgedUptime = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "iotedged_uptime",
                "How long the host has been on",
                new List<string> { }));

            this.usedSpace = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
                "available_disk_space_bytes",
                "Amount of space left on the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.totalSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_space_bytes",
                "Size of the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.usedMemory = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
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
                "created_pids",
                "The number of processes or threads the container has created",
                new List<string> { "module" }));
        }

        public void Start(ILogger logger)
        {
            if (ApiVersion.ParseVersion(this.apiVersion).Value >= ApiVersion.Version20191105.Value)
            {
                this.updateResources = new PeriodicTask(this.Update, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), logger, "Get system resources");
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
            Console.WriteLine($"\n\n\n{JsonConvert.SerializeObject(systemResources.ModuleStats)}\n\n\n");

            this.hostUptime.Set(systemResources.HostUptime, new string[] { });
            this.iotedgedUptime.Set(systemResources.IotEdgedUptime, new string[] { });
            this.usedMemory.Update(systemResources.UsedRam, new string[] { "host" });
            this.totalMemory.Set(systemResources.TotalRam, new string[] { "host" });

            foreach (Disk disk in systemResources.Disks)
            {
                this.usedSpace.Update(disk.AvailableSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
                this.totalSpace.Set(disk.TotalSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
            }

            this.SetModuleStats(systemResources);
        }

        void SetModuleStats(SystemResources systemResources)
        {
            foreach (ModuleStats module in systemResources.ModuleStats)
            {
                var tags = new string[] { module.module };
                // TODO see about double histograms
                this.cpuPercentage.Update((long)(this.GetCpuUsage(module) * 10000), tags);
                this.totalMemory.Set(module.stats.memory_stats.limit, tags);
                this.usedMemory.Update((long)module.stats.memory_stats.usage, tags);
                this.createdPids.Set(module.stats.pids_stats.current, tags);

                Console.WriteLine($"{module.module} cpu: {this.GetCpuUsage(module) * 100}.2f %");
                Console.WriteLine($"{module.module} memory: {module.stats.memory_stats.usage / (double)module.stats.memory_stats.limit}.2f %");
            }
        }

        double GetCpuUsage(ModuleStats module)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                double moduleDiff = module.stats.cpu_stats.cpu_usage.total_usage - module.stats.precpu_stats.cpu_usage.total_usage;
                double systemDiff = module.stats.cpu_stats.system_cpu_usage - module.stats.precpu_stats.system_cpu_usage;

                return moduleDiff / systemDiff;
            }
            else
            {
                double totalIntervals = (module.stats.read - module.stats.preread).TotalMilliseconds * 10; // Get number of 100ns intervals during read
                ulong intervalsUsed = module.stats.cpu_stats.cpu_usage.total_usage - module.stats.precpu_stats.cpu_usage.total_usage;

                if (totalIntervals > 0)
                {
                    return intervalsUsed / totalIntervals;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}
