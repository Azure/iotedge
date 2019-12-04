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
    using Newtonsoft.Json;

    class MetricsScrapeAndUpload : IDisposable
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly MetricsScraper scraper;
        readonly IMetricsPublisher publisher;
        PeriodicTask periodicScrapeAndUpload;

        Guid guid;

        public MetricsScrapeAndUpload(MetricsScraper scraper, IMetricsPublisher publisher, Guid guid)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
            this.guid = Preconditions.CheckNotNull(guid);
        }

        public void Start(TimeSpan scrapeAndUploadInterval)
        {
            this.periodicScrapeAndUpload = new PeriodicTask(this.ScrapeAndUploadPrometheusMetricsAsync, scrapeAndUploadInterval, scrapeAndUploadInterval, Logger, "Scrape and Upload Metrics");
        }

        public void Dispose()
        {
           this.periodicScrapeAndUpload?.Dispose();
        }

        async Task ScrapeAndUploadPrometheusMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<Metric> metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                foreach (Metric metric in metrics)
                {
                    metric.Tags.Add("guid", this.guid.ToString());
                }

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
