// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    class MetricsScrapeAndUpload : IDisposable
    {
        static readonly ILogger Logger = MetricsUtil.CreateLogger("MetricsCollector");
        readonly IMetricsScraper scraper;
        readonly IMetricsPublisher publisher;
        readonly Option<SortedDictionary<string, string>> additionalTags;
        PeriodicTask periodicScrapeAndUpload;

        public MetricsScrapeAndUpload(IMetricsScraper scraper, IMetricsPublisher publisher, Option<SortedDictionary<string, string>> additionalTags)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
            this.additionalTags = Preconditions.CheckNotNull(additionalTags);
        }

        public void Start(TimeSpan scrapeAndUploadInterval)
        {
            this.periodicScrapeAndUpload = new PeriodicTask(this.ScrapeAndUploadMetricsAsync, scrapeAndUploadInterval, scrapeAndUploadInterval, Logger, "Scrape and Upload Metrics");
        }

        public void Dispose()
        {
           this.periodicScrapeAndUpload?.Dispose();
        }

        async Task ScrapeAndUploadMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<Metric> metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                this.additionalTags.ForEach(tags =>
                {
                    metrics = this.GetTaggedMetrics(metrics, tags);
                });

                await this.publisher.PublishAsync(metrics, cancellationToken);
                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error scraping and uploading metrics");
            }
        }

        IEnumerable<Metric> GetTaggedMetrics(IEnumerable<Metric> metrics, SortedDictionary<string, string> additionalTags)
        {
            foreach (Metric metric in metrics)
            {
                Dictionary<string, string> metricTags = new Dictionary<string, string>(metric.Tags);
                foreach (KeyValuePair<string, string> pair in additionalTags)
                {
                    metricTags[pair.Key] = pair.Value;
                }

                yield return new Metric(metric.TimeGeneratedUtc, metric.Name, metric.Value, metricTags);
            }
        }
    }
}
