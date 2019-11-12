// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Extensions.Configuration;

    public class DiagnosticConfig
    {
        public readonly bool Enabled;
        public readonly string MetricsStoragePath;
        public readonly TimeSpan ScrapeInterval;
        public readonly TimeSpan UploadInterval;

        public DiagnosticConfig(bool enabled, string storagePath, IConfiguration configuration)
        {
            this.Enabled = enabled;
            this.MetricsStoragePath = Path.Combine(storagePath, "metrics");
            this.ScrapeInterval = configuration.GetValue("MetricScrapeInterval", TimeSpan.FromHours(1));
            this.UploadInterval = configuration.GetValue("MetricUploadInterval", TimeSpan.FromDays(1));
        }
    }
}
