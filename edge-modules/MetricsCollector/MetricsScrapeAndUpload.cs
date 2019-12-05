// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class MetricsScrapeAndUpload : IDisposable
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly IMetricsScraper scraper;
        readonly IMetricsPublisher publisher;
        PeriodicTask periodicScrapeAndUpload;
        Guid guid;

        public MetricsScrapeAndUpload(IMetricsScraper scraper, IMetricsPublisher publisher, Guid guid)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
            Preconditions.CheckArgument(guid != Guid.Empty);
            this.guid = guid;
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
                IEnumerable<Metric> metricsWithTags = metrics.Select(metric =>
                {
                    metric.Tags.Add("guid", this.guid.ToString());
                    return metric;
                });

                await this.publisher.PublishAsync(metricsWithTags, cancellationToken);

                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error scraping and uploading metrics");
            }
        }
    }
}
