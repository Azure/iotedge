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
        }

        void CheckCPU(List<Metric> metrics)
        {
            const string cpuMetricName = "edgeAgent_used_cpu_percent";
            var cpuMetrics = metrics.Where(m => m.Name == cpuMetricName);
            this.testReporter.Assert($"{cpuMetricName} metric exists", cpuMetrics.Any(), $"Missing {cpuMetricName}");

            const int numQuantiles = 6; // Our histograms return 6 quantiles: 50, 90, 95, 99, 99.9, 99.99
            var hostCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module == "host").ToDictionary(m => m.Tags["quantile"], m => m.Value);
            this.testReporter.Assert("Host has all quantiles", hostCpu.Count == numQuantiles, $"Host had the following quantiles: {string.Join(", ", hostCpu.Keys)}");

            var moduleCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module != "host").ToList();
            this.testReporter.Assert("At least 1 docker module reports cpu", moduleCpu.Any(), $"No modules reported cpu");

            foreach (var hostCpuQuartile in hostCpu)
            {
                this.testReporter.Assert($"{hostCpuQuartile.Key} host CPU <= 100% and >= 0%", hostCpuQuartile.Value <= 100 && hostCpuQuartile.Value >= 0);

                var moduleQuartile = moduleCpu.Where(m => m.Tags["quantile"] == hostCpuQuartile.Key);
                double totalModuleCpu = 0;
                foreach (var module in moduleQuartile)
                {
                    this.testReporter.Assert($"{hostCpuQuartile.Key} {module.Tags["module"]} CPU <= {hostCpuQuartile.Key} host CPU", module.Value <= hostCpuQuartile.Value);
                    totalModuleCpu += module.Value;
                }

                this.testReporter.Assert($"Sum of {hostCpuQuartile.Key} modules' cpu < host", totalModuleCpu < hostCpuQuartile.Value, $"Module cpu values were: {string.Join(", ", moduleQuartile.Select(m => $"{m.Tags["module"]}:{m.Value}"))}");
            }
        }
    }
}
