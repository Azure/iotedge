// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    public class SystemResourcesMetrics
    {
        IMetricsHistogram usedSpace;
        IMetricsGauge totalSpace;

        IMetricsHistogram usedMemory;
        IMetricsGauge totalMemory;

        public SystemResourcesMetrics(IMetricsProvider metricsProvider)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.usedSpace = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
                "used_disk_space_bytes",
                "Amount of space used on the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.totalSpace = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_disk_space_bytes",
                "Size of the disk",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.usedMemory = Preconditions.CheckNotNull(metricsProvider.CreateHistogram(
                "used_memory_bytes",
                "Amount of RAM used by all processes",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));

            this.totalMemory = Preconditions.CheckNotNull(metricsProvider.CreateGauge(
                "total_memory_bytes",
                "RAM avaliable",
                new List<string> { "disk_name", "disk_filesystem", "disk_filetype" }));
        }
    }
}
