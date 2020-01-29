// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Extensions.Logging;

    public class ValidateHostRanges : TestBase
    {
        readonly string endpoint = Guid.NewGuid().ToString();

        public ValidateHostRanges(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
        }

        protected override async Task Test(CancellationToken cancellationToken)
        {
            List<Metric> metrics = (await this.scraper.ScrapeEndpointsAsync(cancellationToken)).ToList();
            this.CheckCPU(metrics);
            this.CheckMemory(metrics);
        }

        void CheckCPU(List<Metric> metrics)
        {
            const string cpuMetricName = "edgeAgent_used_cpu_percent";
            var cpuMetrics = metrics.Where(m => m.Name == cpuMetricName);
            this.testReporter.Assert($"{cpuMetricName} metric exists", cpuMetrics.Any(), $"Missing {cpuMetricName}");

            var hostCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module == "host").ToDictionary(m => m.Tags["quantile"], m => m.Value);
            this.testReporter.Assert("Host has all quantiles", hostCpu.Count == 6, $"Host had the following quantiles: {string.Join(", ", hostCpu.Keys)}");

            var moduleCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module != "host").ToList();
            this.testReporter.Assert("At least 1 docker module reports cpu", moduleCpu.Any(), $"No modules reported cpu");

            foreach (var hostCpuQuartile in hostCpu)
            {
                this.testReporter.Assert($"{hostCpuQuartile.Key} host CPU < 100%", hostCpuQuartile.Value < 100);

                var moduleQuartile = moduleCpu.Where(m => m.Tags["quantile"] == hostCpuQuartile.Key);
                foreach (var module in moduleQuartile)
                {
                    this.testReporter.Assert($"{hostCpuQuartile.Key} {module.Tags["module"]} CPU <= {hostCpuQuartile.Key} host CPU", module.Value <= hostCpuQuartile.Value);
                }
            }
        }

        void CheckMemory(List<Metric> metrics)
        {
            var avaliableDisk = metrics.Where(m => m.Name == "edgeAgent_available_disk_space_bytes").ToDictionary(m => m.Tags["disk_name"], m => m.Value);
            var totalDisk = metrics.Where(m => m.Name == "edgeAgent_total_disk_space_bytes").ToDictionary(m => m.Tags["disk_name"], m => m.Value);

            foreach (var avaliable in avaliableDisk)
            {
                double total = totalDisk[avaliable.Key];
                this.testReporter.Assert($"Disk {avaliable.Key} total space > avaliable space", total > avaliable.Value, $"\n\tTotal: {total}\n\tAvaliable:{avaliable.Value}");
            }
        }
    }
}
