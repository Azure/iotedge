// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
            foreach (DockerStats module in systemResources.ModuleStats)
            {
                var tags = new string[] { module.name };

                this.cpuPercentage.Update(GetCpuUsage(module), tags);
                this.totalMemory.Set(module.memory_stats.limit, tags);
                this.usedMemory.Set((long)module.memory_stats.usage, tags);
                this.createdPids.Set(module.pids_stats.current, tags);

                this.networkIn.Set(module.networks.Sum(n => n.Value.rx_bytes), tags);
                this.networkOut.Set(module.networks.Sum(n => n.Value.tx_bytes), tags);
                this.diskRead.Set(module.blkio_stats.Sum(io => io.Value.Where(d => d.op == "Read").Sum(d => d.value)), tags);
                this.diskWrite.Set(module.blkio_stats.Sum(io => io.Value.Where(d => d.op == "Write").Sum(d => d.value)), tags);
            }
        }

        static double GetCpuUsage(DockerStats module)
        {
            if (module.precpu_stats == null)
            {
                return 0; // startup doesn't doesn't have a previous
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                double moduleDiff = module.cpu_stats.cpu_usage.total_usage - module.precpu_stats.cpu_usage.total_usage;
                double systemDiff = module.cpu_stats.system_cpu_usage - module.precpu_stats.system_cpu_usage;

                return moduleDiff / systemDiff;
            }
            else
            {
                double totalIntervals = (module.read - module.preread).TotalMilliseconds * 10; // Get number of 100ns intervals during read
                ulong intervalsUsed = module.cpu_stats.cpu_usage.total_usage - module.precpu_stats.cpu_usage.total_usage;

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
