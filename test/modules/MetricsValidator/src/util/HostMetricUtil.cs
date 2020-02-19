// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Util
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;

    public static class HostMetricUtil
    {
        public static async Task<bool> WaitForHostMetrics(IMetricsScraper scraper, CancellationToken cancellationToken)
        {
            TimeSpan maxWaitTime = TimeSpan.FromMinutes(2.5);
            TimeSpan frequency = TimeSpan.FromSeconds(10);

            for (int i = 0; i < maxWaitTime / frequency; i++)
            {
                if ((await scraper.ScrapeEndpointsAsync(cancellationToken)).Any(m => m.Name == "edgeAgent_used_cpu_percent" && m.Tags["module_name"] != "host"))
                {
                    return true;
                }

                await Task.Delay(frequency, cancellationToken);
            }

            return false;
        }
    }
}
