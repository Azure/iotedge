// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        readonly Dictionary<string, string> additionalTags;
        readonly List<Metric> failedUploadMetrics;
        PeriodicTask periodicScrapeAndUpload;

        public MetricsScrapeAndUpload(IMetricsScraper scraper, IMetricsPublisher publisher, Dictionary<string, string> additionalTags)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
            this.additionalTags = Preconditions.CheckNotNull(additionalTags);
            this.failedUploadMetrics = new List<Metric>();
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
            IEnumerable<Metric> scrapedMetrics = Enumerable.Empty<Metric>();
            try
            {
                await this.PublishPreviouslyFailedMetricsAsync(cancellationToken);

                scrapedMetrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                scrapedMetrics = this.GetGuidTaggedMetrics(scrapedMetrics);
                await this.publisher.PublishAsync(scrapedMetrics, cancellationToken);
                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error scraping and uploading metrics");
                this.failedUploadMetrics.AddRange(scrapedMetrics);
            }
        }

        async Task PublishPreviouslyFailedMetricsAsync(CancellationToken cancellationToken)
        {
            int processedCount = 0;
            int batchSize = 200;
            foreach (IEnumerable<Metric> batch in this.failedUploadMetrics.Batch(batchSize))
            {
                try
                {
                    await this.publisher.PublishAsync(batch, cancellationToken);
                    processedCount += batchSize;
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Error uploading metrics from prior scrape");
                    break;
                }
            }

            this.failedUploadMetrics.RemoveRange(0, Math.Min(processedCount, this.failedUploadMetrics.Count));
        }

        IEnumerable<Metric> GetGuidTaggedMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                Dictionary<string, string> metricTags = new Dictionary<string, string>(metric.Tags);
                foreach (KeyValuePair<string, string> pair in this.additionalTags)
                {
                    metricTags[pair.Key] = pair.Value;
                }

                yield return new Metric(metric.TimeGeneratedUtc, metric.Name, metric.Value, metricTags);
            }
        }
    }
}
