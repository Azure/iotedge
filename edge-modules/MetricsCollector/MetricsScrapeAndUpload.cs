// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Logging;

    class MetricsScrapeAndUpload
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly MetricsScraper scraper;
        readonly IMetricsPublisher publisher;

        public MetricsScrapeAndUpload(MetricsScraper scraper, IMetricsPublisher publisher)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
        }

        public async Task ScrapeAndUploadPrometheusMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                List<Metric> metrics = (List<Metric>)await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                await this.publisher.PublishAsync(metrics, cancellationToken);
                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping and uploading metrics: {e}");
            }
        }
    }
}
