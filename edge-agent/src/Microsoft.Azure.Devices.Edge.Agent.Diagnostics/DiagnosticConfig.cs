// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public sealed class DiagnosticConfig
    {
        public readonly bool Enabled;
        public readonly string MetricsStoragePath;
        public readonly TimeSpan ScrapeInterval;
        public readonly TimeSpan UploadInterval;

        public DiagnosticConfig(bool enabled, string storagePath, IConfiguration configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.Enabled = enabled;
            this.MetricsStoragePath = Path.Combine(Preconditions.CheckNotNull(storagePath, nameof(storagePath)), "metrics");
            this.ScrapeInterval = configuration.GetValue("MetricScrapeInterval", TimeSpan.FromHours(1));
            this.UploadInterval = configuration.GetValue("MetricUploadInterval", TimeSpan.FromDays(1));
        }
    }
}
