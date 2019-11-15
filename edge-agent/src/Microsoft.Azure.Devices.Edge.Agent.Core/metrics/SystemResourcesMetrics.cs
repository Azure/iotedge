// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

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
                new List<string> { }));

            this.totalMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_memory_bytes",
                "RAM available",
                new List<string> { }));
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
            Console.WriteLine($"\n\n\n{systemResources.DockerStats}\n\n\n");

            this.hostUptime.Set(systemResources.HostUptime, new string[] { });
            this.iotedgedUptime.Set(systemResources.IotEdgedUptime, new string[] { });
            this.usedMemory.Update(systemResources.UsedRam, new string[] { });
            this.totalMemory.Set(systemResources.TotalRam, new string[] { });

            foreach (Disk disk in systemResources.Disks)
            {
                this.usedSpace.Update(disk.AvailableSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
                this.totalSpace.Set(disk.TotalSpace, new string[] { disk.Name, disk.FileSystem, disk.FileType });
            }
        }
    }
}
