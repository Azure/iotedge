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
            var cpuMetrics = metrics.Where(m => m.Name == "edgeAgent_used_cpu_percent");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(cpuMetrics, Newtonsoft.Json.Formatting.Indented));
            var hostCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module == "host").ToDictionary(m => m.Tags["quantile"], m => m.Value);
            var moduleCpu = cpuMetrics.Where(m => m.Tags.TryGetValue("module", out string module) && module != "host").ToList();

            this.testReporter.Assert("Host has all quantiles", hostCpu.Count == 5, $"Host had the following quantiles: {string.Join(", ", hostCpu.Keys)}");

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
    }
}
