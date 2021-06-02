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
            DockerStats[] modules = JsonConvert.DeserializeObject<DockerStats[]>(systemResources.DockerStats);
            foreach (DockerStats module in modules)
            {
                if (!module.Name.HasValue)
                {
                    continue;
                }

                string name = module.Name.OrDefault().Substring(1); // remove '/' from start of name
                var tags = new string[] { name, EdgeRuntimeModules.Contains(name).ToString() };

                this.GetCpuUsage(module).ForEach(usedCpu =>
                {
                    this.cpuPercentage.Update(usedCpu, tags);
                });
                module.MemoryStats.ForEach(ms =>
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        ms.Limit.ForEach(limit => this.totalMemory.Set(limit, tags));
                        ms.Usage.ForEach(usage =>
                        {
                            double actualUsage = usage - ms.Stats.AndThen(s => s.Cache).GetOrElse(0);
                            this.usedMemory.Set((long)actualUsage, tags);
                        });
                    }
                    else
                    {
                        ms.PrivateWorkingSet.ForEach(mem => this.usedMemory.Set(mem, tags));
                    }
                });

                var tagsNoMsTelemetry = new string[] { name, false.ToString() };
                module.PidsStats.ForEach(ps => ps.Current.ForEach(current => this.createdPids.Set(current, tagsNoMsTelemetry)));

                module.Networks.ForEach(network =>
                {
                    this.networkIn.Set(network.Sum(n => n.Value.RxBytes.OrDefault()), tagsNoMsTelemetry);
                    this.networkOut.Set(network.Sum(n => n.Value.TxBytes.OrDefault()), tagsNoMsTelemetry);
                });

                module.BlockIoStats.ForEach(disk =>
                {
                    this.diskRead.Set(disk.Sum(io => io.Value.Where(d => d.Op.Exists(op => op == "Read")).Sum(d => d.Value.OrDefault())), tagsNoMsTelemetry);
                    this.diskWrite.Set(disk.Sum(io => io.Value.Where(d => d.Op.Exists(op => op == "Write")).Sum(d => d.Value.OrDefault())), tagsNoMsTelemetry);
                });
            }
        }

        Option<double> GetCpuUsage(DockerStats module)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return this.GetCpuLinux(module);
            }
            else
            {
                return this.GetCpuWindows(module);
            }
        }

        // Modeled after https://github.com/docker/cli/blob/v19.03.9/cli/command/container/stats_helpers.go#L166
        Option<double> GetCpuLinux(DockerStats module)
        {
            Option<double> currentTotal = module.CpuStats.AndThen(s => s.CpuUsage).AndThen(s => s.TotalUsage);
            Option<double> previousTotal = module.Name.AndThen(this.previousModuleCpu.GetOption);
            Option<double> moduleDelta = currentTotal.AndThen(curr => previousTotal.Map(prev => curr - prev));

            Option<double> currentSystem = module.CpuStats.AndThen(s => s.SystemCpuUsage);
            Option<double> previousSystem = module.Name.AndThen(this.previousSystemCpu.GetOption);
            Option<double> systemDelta = currentSystem.AndThen(curr => previousSystem.Map(prev => curr - prev));

            // set previous to new current
            module.Name.ForEach(name =>
            {
                currentTotal.ForEach(curr => this.previousModuleCpu[name] = curr);
                currentSystem.ForEach(curr => this.previousSystemCpu[name] = curr);
            });

            return moduleDelta.AndThen(moduleDif => systemDelta.AndThen(systemDif =>
            {
                if (moduleDif >= 0 && systemDif > 0)
                {
                    double result = 100 * moduleDif / systemDif;

                    // Occasionally on startup results in a very large number (billions of percent). Ignore this point.
                    if (result < 100)
                    {
                        return Option.Some(result);
                    }
                }

                return Option.None<double>();
            }));
        }

        // Modeled after https://github.com/docker/cli/blob/v19.03.9/cli/command/container/stats_helpers.go#L185
        Option<double> GetCpuWindows(DockerStats module)
        {
            Option<DateTime> previousRead = module.Name.AndThen(this.previousReadTime.GetOption);
            Option<TimeSpan> timeBetweenReadings = module.Read.AndThen(read => previousRead.Map(preRead => read - preRead));

            // Get 100ns intervals
            Option<long> intervalsPerCpu = timeBetweenReadings.Map(tbr => (long)tbr.TotalMilliseconds * 10000);
            Option<long> possibleIntervals = intervalsPerCpu.AndThen(cpuInt => module.NumProcesses.Map(numProc => cpuInt * numProc));

            Option<double> currentTotal = module.CpuStats.AndThen(s => s.CpuUsage).AndThen(s => s.TotalUsage);
            Option<double> previousTotal = module.Name.AndThen(this.previousModuleCpu.GetOption);
            Option<double> intervalsUsed = currentTotal.AndThen(curr => previousTotal.Map(prev => curr - prev));

            // set previous to new current
            module.Name.ForEach(name =>
            {
                currentTotal.ForEach(curr => this.previousModuleCpu[name] = curr);
                module.Read.ForEach(curr => this.previousReadTime[name] = curr);
            });

            return intervalsUsed.AndThen(used => possibleIntervals.AndThen(possible =>
            {
                if (possible > 0)
                {
                    double result = 100 * used / possible;

                    // Occasionally on startup results in a very large number (billions of percent). Ignore this point.
                    if (result < 100)
                    {
                        return Option.Some(result);
                    }
                }

                return Option.None<double>();
            }));
        }
    }
}
