// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class MetricsScrapeAndUpload : IDisposable
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly MetricsScraper scraper;
        readonly IMetricsPublisher publisher;
        PeriodicTask periodicScrapeAndUpload;

        public MetricsScrapeAndUpload(MetricsScraper scraper, IMetricsPublisher publisher)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
        }

        async Task ScrapeAndUploadPrometheusMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<Metric> metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                await this.publisher.PublishAsync(metrics, cancellationToken);
                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping and uploading metrics: {e}");
            }
        }

        public void Start()
        {
            TimeSpan scrapeAndUploadInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
            this.periodicScrapeAndUpload = new PeriodicTask(this.ScrapeAndUploadPrometheusMetricsAsync, scrapeAndUploadInterval, scrapeAndUploadInterval, Logger, "Scrape and Upload Metrics");
        }

        public void Dispose()
        {
           this.periodicScrapeAndUpload.Dispose();
        }
    }
}
