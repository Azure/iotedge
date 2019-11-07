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
        public bool Enabled;
        public string MetricsStoragePath;
        public TimeSpan ScrapeInterval;
        public TimeSpan UploadInterval;

        public DiagnosticConfig(bool enabled, string storagePath, IConfiguration configuration)
        {
            this.Enabled = enabled;
            this.MetricsStoragePath = Path.Combine("metrics", storagePath);
            this.ScrapeInterval = configuration.GetValue<TimeSpan>("metric_scrape_interval");
            this.UploadInterval = configuration.GetValue<TimeSpan>("metric_upload_interval");
        }
    }
}
